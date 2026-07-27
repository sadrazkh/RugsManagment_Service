/*
 * رفتارهای پایهٔ پوستهٔ اپلیکیشن — بدون وابستگی (نه jQuery، نه Vue).
 * جزیره‌های Vue جداگانه لود می‌شوند؛ این فایل فقط چیزهایی است که همیشه لازم است.
 */
(function () {
    'use strict';

    // ═══════════════════════════════════════════════════════
    // ۱) کلید حالت تاریک
    // ═══════════════════════════════════════════════════════
    // انتخاب اولیه در <head> اعمال شده تا فلاش سفید نداشته باشیم؛ اینجا فقط تعویض.
    function initThemeToggle() {
        var toggle = document.getElementById('theme-toggle');
        if (!toggle) return;

        function syncLabel() {
            var isDark = document.documentElement.classList.contains('dark');
            toggle.setAttribute('aria-label', isDark ? 'تغییر به حالت روشن' : 'تغییر به حالت تاریک');
            toggle.setAttribute('aria-pressed', String(isDark));
        }

        toggle.addEventListener('click', function () {
            var isDark = document.documentElement.classList.toggle('dark');
            try { localStorage.setItem('rugs-theme', isDark ? 'dark' : 'light'); } catch (e) { /* بی‌صدا */ }
            syncLabel();
        });

        syncLabel();
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

        function focusableItems() {
            return Array.prototype.slice.call(
                sidebar.querySelectorAll('a[href], button:not([disabled])')
            ).filter(function (el) { return el.offsetParent !== null; });
        }

        function open() {
            isOpen = true;
            sidebar.classList.remove('translate-x-full');
            backdrop.hidden = false;
            openBtn.setAttribute('aria-expanded', 'true');
            document.body.style.overflow = 'hidden';

            var items = focusableItems();
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
            if (event.key !== 'Tab') return;
            var items = focusableItems();
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
        });

        // اگر پنجره به اندازهٔ دسکتاپ بزرگ شد، وضعیت موبایل را پاک کن
        window.matchMedia('(min-width: 768px)').addEventListener('change', function (e) {
            if (e.matches && isOpen) close();
        });
    }

    // ═══════════════════════════════════════════════════════
    // ۳) پل پیام سرور → Toast کلاینت
    // ═══════════════════════════════════════════════════════
    // TempData["Toast"] در layout به‌صورت یک div مخفی رندر می‌شود؛ اینجا به سیستم
    // Toast (که جزیرهٔ UiShell روی window.rugsUI می‌گذارد) تحویل داده می‌شود.
    function initServerToasts() {
        var nodes = document.querySelectorAll('[data-server-toast]');
        if (nodes.length === 0) return;

        var attempts = 0;
        var timer = setInterval(function () {
            attempts++;
            if (window.rugsUI && window.rugsUI.toast) {
                clearInterval(timer);
                nodes.forEach(function (node) {
                    var kind = node.getAttribute('data-server-toast-kind') || 'info';
                    var fn = window.rugsUI.toast[kind] || window.rugsUI.toast.info;
                    fn(node.getAttribute('data-server-toast'));
                    node.remove();
                });
            } else if (attempts > 40) {
                // جزیره لود نشد (مثلاً جاوااسکریپت بلاک شده) — پیام را ساده نمایش بده
                clearInterval(timer);
                nodes.forEach(function (node) {
                    node.hidden = false;
                    node.textContent = node.getAttribute('data-server-toast');
                    node.className = 'mb-4 rounded-lg bg-success/10 px-4 py-3 text-sm text-success';
                });
            }
        }, 100);
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

    function init() {
        initThemeToggle();
        initMobileNav();
        initServerToasts();
        initFormConfirms();
        initAutoSubmit();
        initServiceWorker();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
