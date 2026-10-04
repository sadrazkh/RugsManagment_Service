/*
 * رفتارهای پایهٔ پوستهٔ اپلیکیشن — بدون وابستگی (نه jQuery، نه Vue).
 * جزیره‌های Vue جداگانه لود می‌شوند؛ این فایل فقط چیزهایی است که همیشه لازم است.
 *
 * CSP اسکریپت و handler درون‌خطی را اجرا نمی‌کند؛ پس هر رفتار Razor این‌جا با یک صفت data-* و یک
 * delegate روی document است:
 *   [data-open-panel="name"] [data-panel-props='{…}']   → باز کردن پنل (rugsUI.openPanel)
 *   [data-open-help="page"]                              → کشوی راهنمای همان صفحه
 *   [data-sheet-open="id"] ← #id[data-sheet]             → برگهٔ انتخاب (Esc، پس‌زمینه و ✕ می‌بندند)
 *   [data-post-json="/api/…"] [data-post-body] [data-post-redirect="/…/{id}"] → یک لمس، یک ساختن
 *   [data-text-scale-option="large"]                     → اندازهٔ نوشته (rugsUI.setTextScale)
 *   [data-preview-simple-set="1|0"]                      → پیش‌نمایش «نمای ساده»ی تنظیم‌کننده (کوکی)
 *   [data-server-toast] [data-server-toast-kind]          → پیام TempData با سطحش
 *   form[data-confirm], [data-autosubmit]
 * این فایل بعد از P1 ویرایش نمی‌شود؛ فازهای بعد همین صفت‌ها را در view خودشان به کار می‌برند.
 */
(function () {
    'use strict';

    // ═══════════════════════════════════════════════════════
    // ۰) کمکی‌ها
    // ═══════════════════════════════════════════════════════

    /**
     * window.rugsUI را UiShell (ماژول Vue) بعد از این فایل می‌سازد؛ کلیکی که زودتر برسد صبر می‌کند تا
     * رویداد «rugs:ui-ready» برسد (حداکثر ۶ ثانیه)، بعد بی‌آن ادامه می‌دهد.
     */
    function whenUi(callback, fallback) {
        if (window.rugsUI) { callback(window.rugsUI); return; }

        var done = false;
        function ready() {
            if (done) return;
            done = true;
            window.removeEventListener('rugs:ui-ready', ready);
            if (window.rugsUI) callback(window.rugsUI);
            else if (fallback) fallback();
        }

        window.addEventListener('rugs:ui-ready', ready);
        window.setTimeout(ready, 6000);
    }

    function csrfToken() {
        var meta = document.querySelector('meta[name="csrf-token"]');
        return meta ? meta.getAttribute('content') || '' : '';
    }

    function parseJsonAttr(element, name) {
        var raw = element.getAttribute(name);
        if (!raw) return {};
        try {
            var value = JSON.parse(raw);
            return value && typeof value === 'object' && !Array.isArray(value) ? value : {};
        } catch (e) {
            console.error('[site] JSON نامعتبر در ' + name, element, e);
            return {};
        }
    }

    function toast(level, message) {
        whenUi(function (ui) {
            var fn = (ui.toast && ui.toast[level]) || (ui.toast && ui.toast.info);
            if (fn) fn(message);
        }, function () {
            window.alert(message);
        });
    }

    var FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

    function focusables(container) {
        return Array.prototype.slice.call(container.querySelectorAll(FOCUSABLE))
            .filter(function (el) { return el.offsetParent !== null; });
    }

    /** حبس Tab داخل یک ظرف (منوی کشویی، برگهٔ انتخاب). */
    function trapTab(event, container) {
        if (event.key !== 'Tab') return;
        var items = focusables(container);
        if (items.length === 0) return;

        var first = items[0];
        var last = items[items.length - 1];

        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    }

    // ═══════════════════════════════════════════════════════
    // ۱) کلید حالت تاریک (داخل منوی کاربر)
    // ═══════════════════════════════════════════════════════
    // انتخاب اولیه در <head> اعمال شده تا فلاش سفید نداشته باشیم؛ اینجا فقط تعویض.
    function initThemeToggle() {
        var toggle = document.getElementById('theme-toggle');
        if (!toggle) return;

        function sync() {
            var isDark = document.documentElement.classList.contains('dark');
            toggle.setAttribute('aria-pressed', String(isDark));
            // دکمهٔ بی‌متن برچسب لازم دارد؛ دکمهٔ متن‌دار («حالت تاریک») با aria-pressed کامل است
            if (!toggle.textContent || !toggle.textContent.trim()) {
                toggle.setAttribute('aria-label', isDark ? 'تغییر به حالت روشن' : 'تغییر به حالت تاریک');
            }
        }

        toggle.addEventListener('click', function () {
            var isDark = document.documentElement.classList.toggle('dark');
            try { localStorage.setItem('rugs-theme', isDark ? 'dark' : 'light'); } catch (e) { /* بی‌صدا */ }
            sync();
        });

        sync();
    }

    // ═══════════════════════════════════════════════════════
    // ۲) منوی کشویی موبایل — دسترس‌پذیر
    // ═══════════════════════════════════════════════════════
    // جایگزین هکِ checkbox قبلی: پشتیبانی از ESC، حبس فوکوس، قفل اسکرول و aria درست.
    function initMobileNav() {
        var sidebar = document.getElementById('app-sidebar');
        var backdrop = document.getElementById('nav-backdrop');
        var openBtn = document.getElementById('nav-open');
        var closeBtn = document.getElementById('nav-close');
        if (!sidebar || !backdrop || !openBtn) return;

        var isOpen = false;

        function open() {
            isOpen = true;
            sidebar.classList.remove('translate-x-full');
            backdrop.hidden = false;
            openBtn.setAttribute('aria-expanded', 'true');
            document.body.style.overflow = 'hidden';

            var items = focusables(sidebar);
            if (items.length) items[0].focus();
        }

        function close() {
            isOpen = false;
            sidebar.classList.add('translate-x-full');
            backdrop.hidden = true;
            openBtn.setAttribute('aria-expanded', 'false');
            document.body.style.overflow = '';
            openBtn.focus();
        }

        openBtn.addEventListener('click', open);
        if (closeBtn) closeBtn.addEventListener('click', close);
        backdrop.addEventListener('click', close);

        document.addEventListener('keydown', function (event) {
            if (!isOpen) return;

            if (event.key === 'Escape') {
                event.preventDefault();
                close();
                return;
            }

            // حبس Tab داخل منو تا فوکوس پشت پوشش تیره گم نشود
            trapTab(event, sidebar);
        });

        // اگر پنجره به اندازهٔ دسکتاپ بزرگ شد، وضعیت موبایل را پاک کن
        window.matchMedia('(min-width: 768px)').addEventListener('change', function (e) {
            if (e.matches && isOpen) close();
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۳) پل پیام سرور → Toast کلاینت (با سطح)
    // ═══════════════════════════════════════════════════════
    // TempData["Toast"] (و سطحش در TempData["ToastLevel"]: success | error | warning | info) در layout به‌صورت
    // یک div مخفی رندر می‌شود؛ اینجا به سیستم Toast (که UiShell روی window.rugsUI می‌گذارد) تحویل داده
    // می‌شود. خطای سرور (مثلاً «اول این فرش را از نگله خارج کنید.») دیگر سبز «موفق» دیده نمی‌شود.
    var SERVER_TOAST_STYLE = {
        success: 'mb-4 rounded-lg bg-success/10 px-4 py-3 text-sm text-success',
        error: 'mb-4 rounded-lg bg-error-container px-4 py-3 text-sm text-on-error-container',
        warning: 'mb-4 rounded-lg bg-warning/10 px-4 py-3 text-sm text-warning',
        info: 'mb-4 rounded-lg bg-surface-container px-4 py-3 text-sm text-on-surface'
    };

    function initServerToasts() {
        var nodes = document.querySelectorAll('[data-server-toast]');
        if (nodes.length === 0) return;

        function levelOf(node) {
            var level = node.getAttribute('data-server-toast-kind') || 'info';
            return SERVER_TOAST_STYLE.hasOwnProperty(level) ? level : 'info';
        }

        whenUi(function (ui) {
            nodes.forEach(function (node) {
                var level = levelOf(node);
                var fn = (ui.toast && ui.toast[level]) || ui.toast.info;
                fn(node.getAttribute('data-server-toast'));
                node.remove();
            });
        }, function () {
            // جزیره لود نشد (مثلاً جاوااسکریپت ماژول بلاک شده) — پیام را ساده در صفحه نشان بده
            nodes.forEach(function (node) {
                var level = levelOf(node);
                node.hidden = false;
                node.setAttribute('role', level === 'error' ? 'alert' : 'status');
                node.textContent = node.getAttribute('data-server-toast');
                node.className = SERVER_TOAST_STYLE[level];
            });
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۴) تأیید حذف روی فرم‌های Razor
    // ═══════════════════════════════════════════════════════
    // به‌جای confirm() بومی، از دیالوگ اپ استفاده می‌کند:
    //   <form data-confirm="حذف گروه؟" data-confirm-message="..." data-confirm-danger>
    function initFormConfirms() {
        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            var confirmed = false;

            form.addEventListener('submit', function (event) {
                if (confirmed) return;
                event.preventDefault();

                var ask = window.rugsUI && window.rugsUI.confirm;
                if (!ask) { confirmed = true; form.submit(); return; }

                ask({
                    title: form.getAttribute('data-confirm'),
                    message: form.getAttribute('data-confirm-message') || undefined,
                    confirmLabel: form.getAttribute('data-confirm-label') || 'تأیید',
                    danger: form.hasAttribute('data-confirm-danger'),
                }).then(function (ok) {
                    if (!ok) return;
                    confirmed = true;
                    form.submit();
                });
            });
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۵) جستجوی زنده با تأخیر (debounce)
    // ═══════════════════════════════════════════════════════
    // ورودی‌های [data-autosubmit] بعد از توقف تایپ، فرم را خودشان ارسال می‌کنند.
    // فرم GET است، پس نتیجه در آدرس می‌نشیند و دکمهٔ back درست کار می‌کند.
    function initAutoSubmit() {
        var DELAY = 450;

        document.querySelectorAll('[data-autosubmit]').forEach(function (input) {
            var form = input.form;
            if (!form) return;

            var timer = null;
            var initialValue = input.value;

            function submitNow() {
                // ارسال بی‌مورد وقتی مقدار عوض نشده (مثلاً کاربر فقط کلیک کرده)
                if (input.value === initialValue) return;
                form.submit();
            }

            input.addEventListener('input', function () {
                window.clearTimeout(timer);
                timer = window.setTimeout(submitNow, DELAY);
            });

            // Enter نباید منتظر تأخیر بماند
            input.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') window.clearTimeout(timer);
                // Escape جستجو را پاک و فهرست کامل را برمی‌گرداند
                if (event.key === 'Escape' && input.value !== '') {
                    event.preventDefault();
                    window.clearTimeout(timer);
                    input.value = '';
                    form.submit();
                }
            });
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۶) Service Worker برای PWA
    // ═══════════════════════════════════════════════════════
    function initServiceWorker() {
        if (!('serviceWorker' in navigator)) return;
        window.addEventListener('load', function () {
            navigator.serviceWorker.register('/sw.js').catch(function () { /* بی‌صدا */ });
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۷) منوی کاربر (دکمهٔ نام در سرصفحه)
    // ═══════════════════════════════════════════════════════
    // الگوی «دکمه + ناحیهٔ بازشونده» (نه role=menu، چون داخلش گروه رادیویی و فرم خروج هست):
    // کلیک باز/بسته، Esc می‌بندد و فوکوس به دکمه برمی‌گردد، کلیک بیرون یا رفتن فوکوس به بیرون می‌بندد.
    function initUserMenu() {
        var button = document.getElementById('user-menu-button');
        var panel = document.getElementById('user-menu');
        if (!button || !panel) return;

        var wrapper = button.closest('[data-user-menu]') || button.parentElement;

        function isOpen() { return !panel.hidden; }

        function open() {
            panel.hidden = false;
            button.setAttribute('aria-expanded', 'true');
            var checked = panel.querySelector('[role="radio"][aria-checked="true"]');
            var target = checked || focusables(panel)[0];
            if (target) target.focus();
        }

        function close(returnFocus) {
            if (!isOpen()) return;
            panel.hidden = true;
            button.setAttribute('aria-expanded', 'false');
            if (returnFocus) button.focus();
        }

        button.addEventListener('click', function () {
            if (isOpen()) close(false); else open();
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && isOpen()) {
                event.preventDefault();
                close(true);
            }
        });

        document.addEventListener('click', function (event) {
            if (isOpen() && !wrapper.contains(event.target)) close(false);
        });

        wrapper.addEventListener('focusout', function (event) {
            if (isOpen() && event.relatedTarget && !wrapper.contains(event.relatedTarget)) close(false);
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۸) اندازهٔ نوشته: [آ] [آ+] [آ++]
    // ═══════════════════════════════════════════════════════
    // گروه رادیویی (role=radiogroup) با پیمایش جهت‌دار RTL: چپ/پایین = بعدی، راست/بالا = قبلی. انتخاب همان
    // لحظه ریشهٔ صفحه را بزرگ می‌کند (data-text-scale) و با PUT /api/me/preferences ذخیره می‌شود
    // (rugsUI.setTextScale؛ اگر ذخیره نشد برمی‌گردد). هر جای دیگری هم (کشوی راهنما) اندازه را عوض کند،
    // این گروه از روی <html data-text-scale> همگام می‌شود.
    function initTextScale() {
        var groups = document.querySelectorAll('[data-text-scale-group]');
        if (groups.length === 0) return;

        var root = document.documentElement;

        function sync() {
            var current = root.getAttribute('data-text-scale') || 'normal';
            groups.forEach(function (group) {
                group.querySelectorAll('[data-text-scale-option]').forEach(function (option) {
                    var selected = option.getAttribute('data-text-scale-option') === current;
                    option.setAttribute('aria-checked', String(selected));
                    option.setAttribute('tabindex', selected ? '0' : '-1');
                });
            });
        }

        function choose(value) {
            if (!value || root.getAttribute('data-text-scale') === value) return;
            whenUi(function (ui) {
                ui.setTextScale(value);
            }, function () {
                // بدون پوستهٔ Vue: همین‌جا اعمال و ذخیره کن
                root.setAttribute('data-text-scale', value);
                var values = { normal: 0, large: 1, xlarge: 2 };
                fetch('/api/me/preferences', {
                    method: 'PUT',
                    headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken() },
                    credentials: 'same-origin',
                    body: JSON.stringify({ textScale: values[value] })
                }).catch(function () { /* بی‌صدا؛ دفعهٔ بعد اندازهٔ ذخیره‌شده برمی‌گردد */ });
            });
        }

        groups.forEach(function (group) {
            group.addEventListener('click', function (event) {
                var option = event.target.closest('[data-text-scale-option]');
                if (option) choose(option.getAttribute('data-text-scale-option'));
            });

            group.addEventListener('keydown', function (event) {
                var keys = { ArrowLeft: 1, ArrowDown: 1, ArrowRight: -1, ArrowUp: -1 };
                if (!keys.hasOwnProperty(event.key)) return;
                event.preventDefault();

                var options = Array.prototype.slice.call(group.querySelectorAll('[data-text-scale-option]'));
                var index = options.indexOf(document.activeElement);
                if (index < 0) index = 0;
                var next = options[(index + keys[event.key] + options.length) % options.length];
                next.focus();
                choose(next.getAttribute('data-text-scale-option'));
            });
        });

        new MutationObserver(sync).observe(root, { attributes: true, attributeFilter: ['data-text-scale'] });
        sync();
    }

    // ═══════════════════════════════════════════════════════
    // ۹) پیش‌نمایش «نمای ساده»ی تنظیم‌کننده
    // ═══════════════════════════════════════════════════════
    // فقط یک کوکی (rugs.preview-simple=1)؛ سرور آن را فقط برای تنظیم‌کننده می‌خواند و فقط پوسته (منو و
    // کنترل‌های پنهان) را عوض می‌کند، هرگز دسترسی را. پس از تغییر، صفحه دوباره ساخته می‌شود.
    function initPreviewSimple() {
        document.addEventListener('click', function (event) {
            var button = event.target.closest('[data-preview-simple-set]');
            if (!button) return;
            event.preventDefault();

            var on = button.getAttribute('data-preview-simple-set') === '1';
            var secure = location.protocol === 'https:' ? '; secure' : '';
            document.cookie = 'rugs.preview-simple=' + (on ? '1; max-age=31536000' : '; max-age=0') +
                '; path=/; samesite=lax' + secure;
            location.reload();
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۱۰) پنل‌ها و راهنما از دکمه‌های Razor
    // ═══════════════════════════════════════════════════════
    //   <button type="button" data-open-panel="cost-entry" data-panel-props='{"scope":2}'>…</button>
    //   <button type="button" data-open-help="rugs">راهنما</button>
    function initPanelTriggers() {
        document.addEventListener('click', function (event) {
            var panelTrigger = event.target.closest('[data-open-panel]');
            if (panelTrigger) {
                event.preventDefault();
                var name = panelTrigger.getAttribute('data-open-panel');
                var props = parseJsonAttr(panelTrigger, 'data-panel-props');
                whenUi(function (ui) {
                    // فوکوس روی دکمه بماند تا پنل پس از بسته شدن به همین‌جا برگرداند
                    panelTrigger.focus();
                    ui.openPanel(name, props);
                }, function () {
                    window.alert('این بخش بارگذاری نشد. صفحه را تازه کنید.');
                });
                return;
            }

            var helpTrigger = event.target.closest('[data-open-help]');
            if (helpTrigger) {
                event.preventDefault();
                var page = helpTrigger.getAttribute('data-open-help') || '';
                whenUi(function (ui) { ui.openHelp(page); }, function () { location.href = '/Help/Card'; });
            }
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۱۱) برگهٔ انتخاب (مثل «کدام محموله؟» در خانه)
    // ═══════════════════════════════════════════════════════
    //   <button data-sheet-open="sheet-id" aria-controls="sheet-id">…</button>
    //   <div id="sheet-id" role="dialog" aria-modal="true" data-sheet hidden>
    //       <div data-sheet-close></div>  (پس‌زمینه)  …  <a data-sheet-autofocus="true">…</a>
    //   </div>
    function initSheets() {
        var openSheet = null;
        var opener = null;

        function close() {
            if (!openSheet) return;
            openSheet.hidden = true;
            openSheet = null;
            document.body.style.overflow = '';
            if (opener) opener.focus();
            opener = null;
        }

        document.addEventListener('click', function (event) {
            var trigger = event.target.closest('[data-sheet-open]');
            if (trigger) {
                var sheet = document.getElementById(trigger.getAttribute('data-sheet-open'));
                if (!sheet) return;
                event.preventDefault();
                close();
                opener = trigger;
                openSheet = sheet;
                sheet.hidden = false;
                document.body.style.overflow = 'hidden';
                var target = sheet.querySelector('[data-sheet-autofocus="true"]') || focusables(sheet)[0];
                if (target) target.focus();
                return;
            }

            if (openSheet && event.target.closest('[data-sheet-close]')) {
                event.preventDefault();
                close();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (!openSheet) return;
            if (event.key === 'Escape') {
                event.preventDefault();
                close();
                return;
            }
            trapTab(event, openSheet);
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۱۲) یک لمس، یک ساختن: [data-post-json]
    // ═══════════════════════════════════════════════════════
    //   <button data-post-json="/api/shipments" data-post-body='{"documentDate":"2026-09-06"}'
    //           data-post-redirect="/Shipments/{id}?tab=bales">محمولهٔ تازه</button>
    // POST با توکن CSRF؛ موفق → رفتن به data-post-redirect («{id}» با id پاسخ پر می‌شود)، یا تازه‌سازی صفحه.
    // خطا → پیام فارسی سرور. دو لمس پشت‌سرهم دو چیز نمی‌سازد (دکمه تا پاسخ قفل است).
    function initPostButtons() {
        document.addEventListener('click', function (event) {
            var button = event.target.closest('[data-post-json]');
            if (!button) return;
            event.preventDefault();
            if (button.getAttribute('aria-busy') === 'true') return;

            var url = button.getAttribute('data-post-json');
            var body = parseJsonAttr(button, 'data-post-body');
            var redirect = button.getAttribute('data-post-redirect');

            button.setAttribute('aria-busy', 'true');
            button.setAttribute('aria-disabled', 'true');

            fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrfToken() },
                credentials: 'same-origin',
                body: JSON.stringify(body)
            }).then(function (res) {
                return res.text().then(function (text) {
                    var data = null;
                    try { data = text ? JSON.parse(text) : null; } catch (e) { data = null; }
                    if (!res.ok) throw new Error((data && data.message) || 'کار انجام نشد؛ دوباره امتحان کنید.');
                    return data;
                });
            }).then(function (data) {
                var id = data && (data.id || data.Id);
                if (redirect && (id || redirect.indexOf('{id}') < 0)) {
                    location.href = id ? redirect.replace('{id}', encodeURIComponent(id)) : redirect;
                } else {
                    // پاسخ بی‌id برای مقصدی که id می‌خواهد: همین صفحه را تازه کن، نه نشانیِ «{id}»
                    location.reload();
                }
            }).catch(function (error) {
                button.removeAttribute('aria-busy');
                button.removeAttribute('aria-disabled');
                var message = error instanceof TypeError
                    ? 'ذخیره نشد؛ اینترنت را نگاه کنید.'
                    : (error && error.message) || 'کار انجام نشد؛ دوباره امتحان کنید.';
                toast('error', message);
            });
        });
    }

    function init() {
        initThemeToggle();
        initMobileNav();
        initServerToasts();
        initFormConfirms();
        initAutoSubmit();
        initServiceWorker();
        initUserMenu();
        initTextScale();
        initPreviewSimple();
        initPanelTriggers();
        initSheets();
        initPostButtons();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
