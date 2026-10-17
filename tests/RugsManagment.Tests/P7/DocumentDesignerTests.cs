using System.Text.Json.Nodes;
using RugsManagment.Application.Common;
using RugsManagment.Application.Features.Documents.Composer;
using RugsManagment.Application.Features.Documents.Data;
using RugsManagment.Application.Features.Documents.Layout;
namespace RugsManagment.Tests.P7;
public sealed class DocumentDesignerTests
{
    [Fact]
    public void CostTemplate_AmountsArePerLine_AndDoNotMultiplyLotsAgain()
    {
        var layout = LayoutJson.Default("shipment-cost").Parse();
        var rug = new DocumentRug(Guid.NewGuid(),"1 - 50",50,Money:new Dictionary<string,decimal?> {
            ["rug.purchaseCost"] = 1000m,["rug.stepCost"] = 200m,["rug.expenseCost"] = 300m,["rug.totalCost"] = 1500m,["rug.costPerM2"] = 187.5m });
        var data = new DocumentData(new DocumentShipment(Guid.NewGuid(),1,"هزینه",new DateOnly(2026,10,17),null,null,null,null,null),
            [new DocumentBale(1,[new DocumentLine(1,rug,0.4m,0.4m)])],[],BaleTerms.Negleh,new DocumentTenant(),false);
        Assert.True(LayoutValidator.Validate(layout,DocumentFields.Catalogue()).UsesMoneyFields);
        var rendered = DocumentComposer.Compose(layout,data);
        var totalColumn = rendered.Sheets[0].Columns.Single(x => x.Key == "total").Index;
        var amount = rendered.Sheets[0].Rows.Single(x => x.Kind == RenderRowKinds.Footer).Cells.Single(x => x.Col == totalColumn);
        Assert.Equal(1500m,amount.Value.Number); Assert.StartsWith("SUM(",amount.Formula);
    }
    [Fact]
    public void MovingFooterAnchor_KeepsSummaryFormulaBoundToItsId()
    {
        var node = JsonNode.Parse(LayoutJson.Default("shipment-declaration").Json)!;
        var footer = node["sheets"]![0]!["group"]!["footer"]!.AsArray();
        var usd = footer.Single(x => x!["id"]!.GetValue<string>() == "usd")!;
        var area = footer.Single(x => x!["id"]!.GetValue<string>() == "area")!;
        // جابه‌جایی دو خانه با پهنای برابر تداخل ایجاد نمی‌کند.
        usd["col"] = "width"; area["col"] = "rate";
        var catalogue = DocumentFields.Catalogue([DocumentFields.CustomText("bg_color","رنگ متن"),DocumentFields.CustomText("border_color","رنگ حاشیه")]);
        var layout = LayoutJson.Parse(node.ToJsonString()).Layout!;
        Assert.True(LayoutValidator.Validate(layout,catalogue).Ok);
        var rug = new DocumentRug(null,"1",1);
        var data = new DocumentData(new DocumentShipment(null,1,null,new DateOnly(2026,10,17),null,null,null,null,null),
            [new DocumentBale(1,[new DocumentLine(1,rug,2,3,UsdPerM2:45m)])],[],BaleTerms.Negleh,new DocumentTenant(),false);
        var render = DocumentComposer.Compose(layout,data,new ComposeOptions(Fields:catalogue));
        var summary = render.Sheets.Single(x => x.Role == "summary");
        Assert.Contains(summary.Rows.SelectMany(x => x.Cells),c => c.Formula is not null && c.Formula.Contains("I4",StringComparison.Ordinal) && c.Value.Number == 270m);
    }
}
