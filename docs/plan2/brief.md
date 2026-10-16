# Design brief — "simple but all-capable" export rug workflow for RugsManagment_Service

## 1. The owner's request (verbatim Persian, then faithful English)

> ببین این یکم هم پیچیدس
> میخواهم کلیت راحتی هم داشته باشه که اینشکلی باشه مثلا به محض ورود به سیستم اندازه و اطلاعات ثبت بشه و قیمت خرید هرچی لازمه داینامیک بشه اضافه کم کرد و زمان عدل بندی هم همینطور مشخصات نهایی که ممکنه اندازه کم و زیاد شده باشه برای هرفرش ثبت بشه
> بعد یه لیست بیاره هزینه هارو بزنیم ممکنه هزینه رو بزنیم به ازای هر متر یا بزنیم کل فرش ها یا این فرش ها کلشون باهم این هزینه و بتونه همه رو محاسبه و کم و زیاد کنه همه امکانات همه جایگشت ها ساپورت بشن و داینامیک باشه
> و مثلا درنهایت حتی بشه خروجی رو هم طراحی کرد و خروجی بده بهمون مثلا خروجی نمونه تا الان رو اکسل این بوده که میفرستم این صرفا یه نمونس
> برو کامل درست کن بهترین چیزی که میتونه باشه هم ساده هم پرامکانات و همه فن حریف اینم در نظر بگیر شخصی که استفاده میکنه مهندس نیست یه بازاریه سادس ولی خب تنظیمات رو میتونم من براش یبار انجام بدم همه اینارو در نظر بگیرui/ux رو هم بهوبد بده

English: "Look, this [current system] is a bit complicated. I want the overall flow to be easy, like this: as soon as a rug enters the system its size and info get recorded, and the purchase price — whatever is needed should be dynamic, add/remove [components]. Same at baling time [عدل‌بندی]: the final specs — the size may have shrunk or grown — get recorded for each rug. Then it brings up a list where we enter the costs: we might enter a cost per metre, or for all the rugs, or 'these rugs all together have this cost', and it must be able to compute everything and adjust it — all features, all permutations supported, dynamic. And in the end we should even be able to DESIGN the output and have it produce the output for us — e.g. the output so far was this Excel I'm sending, it's just a sample. Go build it completely, the best thing it can be: simple AND feature-rich, a jack-of-all-trades. Keep in mind the person using it is not an engineer, he's a simple bazaar merchant — but I (his son) can do the settings for him once. Consider all of this; improve the UI/UX too."

So there are two personas:
- **The merchant** (the owner's father, elderly bazaar rug exporter, non-technical, used to Excel and paper). Daily user. Must find it obvious.
- **The configurator** (the owner, technical). Sets up fields, cost categories, value tables, numbering, output templates ONCE.

## 2. The sample output (their current Excel) — full dump at scratchpad/xlsx/dump.txt

File: `C:\Users\sadra\Desktop\baba-1404-08-11 (2) (2).xlsx` (one RTL sheet). Read `docs/plan2/xlsx\dump.txt` for every cell with its formula.

It is the **export packing list + customs declaration** ("صورت نگله بندی و اظهار فرش صادراتی  تاریخ 1405.06.15"):
- Header row: نگله (bale #) | شماره (merchant rug serial, e.g. 3778) | رنگ متن (field colour) | رنگ حاشیه (border colour) | نقشه (design) | محل بافت (weaving origin/type: گبه / خراسان تجاری / نائین 9 لا) | مواد مصرفی (material: پشم) | طول (length m) | عرض (width m) | مساحت (=ROUND(H*I,2)) | ارزش متر مربع (declared customs value USD per m²: 45 gabbeh, 25 Khorasan commercial, 80 Naein 9-la) | جمع کل دلار (=ROUND(J*K,2)).
- Bale column A is merged vertically over the bale's rows.
- After each bale a footer row: [A:B] "نگله شماره" [C] bale# [D] "شامل" [E] piece count [F] "تخته" [I:J] SUM(area) [K:L] SUM(USD).
- 36 bales of ~12–16 m² each (the merchant packs to a target area). 242 pieces, 464.74 m², 20,322.50 USD in total.
- **Lots:** one line can stand for N identical small pieces: serial range "3911 - 3960" (50 pcs of 0.40×0.40), "3961 - 3984" (24 pcs), "3985 - 4003" (range spans 19 numbers but quantity is 10 — so range length must NOT be assumed to equal quantity) — the quantity was typed into the material column; the bale footer multiplies by quantity by hand.
- The title + header repeat at every printed page (A4 portrait, fit to width, font "B Nazanin", margins 0.2"/0.5").
- Last page: summary table per bale: نگله | تعداد فرش | متراژ | ارزش گمرکی, then grand totals (= row).
- Hand-made errors the system must make impossible: bale 33's footer says "26"; a SUM range spills over another bale; a rug sits between two bales; summary numbers are typed, not linked; headers re-typed at page breaks; lot quantity typed into the material column.

Note: the declared value (USD/m²) is NOT the purchase cost. Internal costs (purchase, washing, repair, transport, customs fees, packing…) are tracked in Toman (tenant currency) for the merchant's own profit/cost-per-metre picture. Both must coexist; USD must never be mixed into Toman money fields.

## 3. Capabilities required (all must be covered)
1. **Intake** — register size + info the moment a rug arrives; dynamic fields (configured once); purchase price made of dynamic components (add/remove lines: e.g. price per m² × area, broker fee, transport-in, …).
2. **Baling** — pack rugs into numbered bales inside an export shipment/declaration; record the FINAL dimensions per rug at baling (size changes after washing); live per-bale totals (pieces, m², USD) so he can pack to a target area; lots (qty N, serial range).
3. **Cost list** — enter costs per m², per piece, per rug, or a lump sum for a set of rugs (selection / bale / shipment / intake batch / all / filter) that is allocated (by area / by count / equally / by value / manual); everything recomputes when anything changes (dims, membership, the cost itself); per-rug and per-m² cost visibility; profit when sold.
4. **Output designer** — the configurator designs document templates (columns, labels, computed columns, grouping by bale with subtotal rows, per-group summary table, grand totals, title/header placeholders like the date, page setup); the merchant just picks a shipment and clicks "Excel" or "Print". Real .xlsx (RTL, merged cells, live formulas like the sample) + print/PDF. The default template must reproduce the sample sheet faithfully (minus its errors).
5. **Lots** — one line = N identical pieces with a serial range.
6. **Merchant serial numbers** (3778, 3779, …) besides the system SKU; auto-suggest next number.
7. **Easy data entry** — spreadsheet-like quick entry grid (keyboard + paste from Excel), Excel import of their existing sheets (with the quirks above), and a much simpler, merchant-friendly UI/UX overall (Persian, big targets, plain words, few concepts). Existing advanced features (workflow steps, providers, labels, sales, analytics) must keep working but must not get in the merchant's way.

## 4. Codebase facts
Read the consolidated architecture map: `docs/plan2/map.md` (741 lines, with file:line references). The six raw reader reports are in `...\scratchpad\reads.json` if you need more depth. The repo is at `C:\Users\sadra\source\repos\RugsManagment_Service` — you may read any source file to verify, but you must NOT modify anything.

Key facts to respect (details in map §5 and §8):
- ASP.NET Core 10 MVC + Vue 3 islands (no SPA, no router, CSP nonce-only scripts, no CDN scripts), EF Core 10 + Npgsql, Clean Architecture (no EF in Application), manual tenant isolation (404 cross-tenant), soft-delete query filters, xmin concurrency, Persian/Jalali/Persian digits display, Tailwind v4 tokens ("Heritage Loom" palette, dark mode), icon sprite (no emoji).
- The migration chain is broken and `MigrateAsync` is commented out (map §8 #1–2) — must be fixed before any new migration.
- TotalInvestment is duplicated in 7 places (map §2.2); any new cost source must be integrated consistently (strongly consider a single denormalised/centralised total).
- Rounding today is inconsistent (ToEven vs Excel away-from-zero); area is unrounded in the domain while the sample uses ROUND(L×W,2) per line.
- ClosedXML 0.105.1 (MIT) is available from nuget.org and is the intended xlsx read/write library (Infrastructure, behind an Application abstraction).
- Working agreement: the user approves each phase, the user does all git commits. Persian text must never be sent through the Git Bash terminal (use the browser fetch path for seeding/testing Persian data).
- Uncommitted cleanup from the previous session was lost in a repo move (JWT dead code removal, FluentValidation/@vueuse removal, AsSplitQuery on multi-collection loads, sw.js cache versioning + PNG PWA icons, postcss audit fix, untracking the committed data-protection key, docker-compose password alignment, README refresh). These may be re-applied as a prerequisite phase.

## 5. Non-goals / guardrails
- Do not delete the workflow/provider/label/sales/analytics features; re-home them as "advanced" so the merchant's flow stays clean.
- No new external services; everything server-side in this app; no CDN scripts.
- Keep every existing URL working or redirect it.

