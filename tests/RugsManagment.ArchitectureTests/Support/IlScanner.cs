using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace RugsManagment.ArchitectureTests.Support;

/// <summary>یک فراخوانی در IL: چه کسی (روش منطقی) چه متدی را صدا زد.</summary>
/// <param name="CallerType">نوعی که روش منطقی در آن تعریف شده (نه نوع ساختگی ماشین حالت/lambda).</param>
/// <param name="LogicalMethod">نام روش منطقی (مثلاً «SaveAsync» برای ماشین حالت <c>&lt;SaveAsync&gt;d__5</c>).</param>
/// <param name="Callee">متد صدا زده‌شده (با آرگومان‌های عام بسته، اگر در دسترس بود).</param>
public sealed record IlCall(Type CallerType, string LogicalMethod, MethodBase Callee)
{
    public override string ToString() => $"{CallerType.FullName}.{LogicalMethod} → {Callee.DeclaringType?.Name}.{Callee.Name}";
}

/// <summary>
/// خوانندهٔ کوچک IL برای آزمون‌های معماری: همهٔ <c>call</c>/<c>callvirt</c>/<c>newobj</c>/<c>ldftn</c> هر متد را با
/// <see cref="Module.ResolveMethod(int, Type[], Type[])"/> پیدا می‌کند و به «روش منطقی» نسبت می‌دهد — کد async (ماشین حالت)،
/// lambda و تابع محلی به همان متدی برمی‌گردند که در منبع نوشته شده است.
///
/// چرا IL و نه فقط متن منبع: فراخوانی‌های پنهان پشت extension methodها (ExecuteUpdate، ExecuteSqlRaw) یا ارث‌بری
/// (<c>Repository&lt;Rug&gt;.Update</c>) در متن یک نام ساده‌اند؛ در IL متد دقیق با نوع عامش دیده می‌شود.
/// </summary>
public static partial class IlScanner
{
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>همهٔ فراخوانی‌های همهٔ متدهای این اسمبلی‌ها.</summary>
    public static IReadOnlyList<IlCall> Calls(params Assembly[] assemblies)
    {
        var calls = new List<IlCall>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in SafeTypes(assembly))
            {
                foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
                {
                    var (callerType, logical) = Logical(type, method.Name);
                    foreach (var callee in CalledMethods(method))
                        calls.Add(new IlCall(callerType, logical, callee));
                }
            }
        }

        return calls;
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    /// <summary>
    /// نوع و نام منطقی: ماشین حالت <c>&lt;X&gt;d__N</c> و کلاس‌های ساختگی lambda (<c>&lt;&gt;c</c>،
    /// <c>&lt;&gt;c__DisplayClassN_M</c>) به نوع بیرونی برمی‌گردند؛ نام متدِ <c>&lt;X&gt;b__N_M</c> یا
    /// <c>&lt;X&gt;g__Local|N_M</c> به X.
    /// </summary>
    private static (Type Type, string Method) Logical(Type type, string methodName)
    {
        var current = type;
        string? logical = null;
        while (current.IsNested && current.Name.StartsWith('<'))
        {
            var match = CompilerName().Match(current.Name);
            if (match.Success && match.Groups[1].Value.Length > 0)
                logical ??= match.Groups[1].Value;
            current = current.DeclaringType!;
        }

        var methodMatch = CompilerName().Match(methodName);
        if (methodMatch.Success && methodMatch.Groups[1].Value.Length > 0)
            logical = methodMatch.Groups[1].Value;

        return (current, logical ?? methodName);
    }

    [GeneratedRegex(@"^<([^>]*)>")]
    private static partial Regex CompilerName();

    private static IEnumerable<MethodBase> CalledMethods(MethodBase method)
    {
        byte[]? il;
        try
        {
            il = method.GetMethodBody()?.GetILAsByteArray();
        }
        catch (Exception)
        {
            yield break;
        }

        if (il is null)
            yield break;

        var module = method.Module;
        var typeArgs = method.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } mi ? mi.GetGenericArguments() : null;

        var position = 0;
        while (position < il.Length)
        {
            short value = il[position++];
            if (value == 0xFE)
                value = (short)(0xFE00 | il[position++]);
            if (!OpCodesByValue.TryGetValue(value, out var opCode))
                yield break; // IL ناشناخته (نباید رخ دهد)؛ بقیهٔ متد را رها می‌کنیم

            if (opCode.OperandType == OperandType.InlineMethod)
            {
                var token = BitConverter.ToInt32(il, position);
                MethodBase? callee = null;
                try
                {
                    callee = module.ResolveMethod(token, typeArgs, methodArgs);
                }
                catch (Exception)
                {
                    // متدی از اسمبلی‌ای که بار نمی‌شود؛ برای این آزمون‌ها مهم نیست
                }

                if (callee is not null)
                    yield return callee;
            }

            position += OperandSize(opCode, il, position);
        }
    }

    private static int OperandSize(OpCode opCode, byte[] il, int position) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, position) * 4,
        _ => 4
    };
}
