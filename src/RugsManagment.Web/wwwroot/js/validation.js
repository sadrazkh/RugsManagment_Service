/*
 * اعتبارسنجی سمت کلاینت — جایگزین jQuery + jquery-validation-unobtrusive.
 *
 * همان صفت‌های data-val-* را می‌خواند که تگ‌هلپرهای ASP.NET Core تولید می‌کنند،
 * بنابراین viewها بدون تغییر کار می‌کنند. سرور همچنان ModelState را اعتبارسنجی
 * می‌کند؛ این فقط بازخورد فوری به کاربر می‌دهد.
 *
 * پشتیبانی: required, email, regex, minlength, maxlength, length, range, compare
 */
(function () {
    'use strict';

    /** پیام خطای یک قانون را برمی‌گرداند؛ اگر قانون روی فیلد نباشد null. */
    function ruleMessage(input, rule) {
        return input.getAttribute('data-val-' + rule);
    }

    /** یک فیلد را می‌سنجد و اولین پیام خطا (یا null) را برمی‌گرداند. */
    function validateField(input) {
        if (input.getAttribute('data-val') !== 'true') return null;
        if (input.disabled || input.readOnly) return null;

        var value = input.type === 'checkbox' ? (input.checked ? 'true' : '') : (input.value || '').trim();

        var requiredMsg = ruleMessage(input, 'required');
        if (requiredMsg && value === '') return requiredMsg;

        // بقیهٔ قوانین فقط وقتی مقدار وجود دارد اعمال می‌شوند (فیلد اختیاری خالی معتبر است)
        if (value === '') return null;

        var emailMsg = ruleMessage(input, 'email');
        if (emailMsg && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) return emailMsg;

        var regexMsg = ruleMessage(input, 'regex');
        if (regexMsg) {
            var pattern = input.getAttribute('data-val-regex-pattern');
            if (pattern && !new RegExp('^(?:' + pattern + ')$').test(value)) return regexMsg;
        }

        var minLenMsg = ruleMessage(input, 'minlength');
        if (minLenMsg && value.length < Number(input.getAttribute('data-val-minlength-min'))) return minLenMsg;

        var maxLenMsg = ruleMessage(input, 'maxlength');
        if (maxLenMsg && value.length > Number(input.getAttribute('data-val-maxlength-max'))) return maxLenMsg;

        var lengthMsg = ruleMessage(input, 'length');
        if (lengthMsg) {
            var min = input.getAttribute('data-val-length-min');
            var max = input.getAttribute('data-val-length-max');
            if ((min !== null && value.length < Number(min)) || (max !== null && value.length > Number(max))) return lengthMsg;
        }

        var rangeMsg = ruleMessage(input, 'range');
        if (rangeMsg) {
            var num = Number(value);
            var lo = Number(input.getAttribute('data-val-range-min'));
            var hi = Number(input.getAttribute('data-val-range-max'));
            if (Number.isNaN(num) || num < lo || num > hi) return rangeMsg;
        }

        var compareMsg = ruleMessage(input, 'equalto');
        if (compareMsg) {
            var otherSel = input.getAttribute('data-val-equalto-other');
            if (otherSel) {
                // فرمت ASP.NET: "*.OtherField" → نام فیلد را جدا می‌کنیم
                var otherName = otherSel.replace(/^\*\./, '');
                var other = input.form && input.form.querySelector('[name="' + otherName + '"]');
                if (other && other.value !== input.value) return compareMsg;
            }
        }

        return null;
    }

    /** پیام خطا را در span متناظر (data-valmsg-for) نشان می‌دهد و aria را تنظیم می‌کند. */
    function showError(input, message) {
        var name = input.getAttribute('name');
        var span = input.form && input.form.querySelector('[data-valmsg-for="' + name + '"]');
        if (span) span.textContent = message || '';

        if (message) {
            input.setAttribute('aria-invalid', 'true');
            if (span && span.id) input.setAttribute('aria-describedby', span.id);
        } else {
            input.removeAttribute('aria-invalid');
        }
    }

    function fieldsOf(form) {
        return Array.prototype.slice.call(form.querySelectorAll('[data-val="true"]'));
    }

    function wireForm(form) {
        var fields = fieldsOf(form);
        if (fields.length === 0) return;

        // اعتبارسنجی هنگام خروج از فیلد (blur) — نه هنگام تایپ، تا کاربر آزار نبیند
        fields.forEach(function (input) {
            input.addEventListener('blur', function () {
                showError(input, validateField(input));
            });
            // بعد از اولین خطا، هنگام تایپ خطا را زنده پاک می‌کنیم
            input.addEventListener('input', function () {
                if (input.getAttribute('aria-invalid') === 'true') showError(input, validateField(input));
            });
        });

        form.addEventListener('submit', function (event) {
            var firstInvalid = null;
            fields.forEach(function (input) {
                var message = validateField(input);
                showError(input, message);
                if (message && !firstInvalid) firstInvalid = input;
            });

            if (firstInvalid) {
                event.preventDefault();
                firstInvalid.focus();
                firstInvalid.scrollIntoView({ block: 'center', behavior: 'smooth' });
            }
        });
    }

    function init() {
        Array.prototype.slice.call(document.querySelectorAll('form')).forEach(wireForm);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
