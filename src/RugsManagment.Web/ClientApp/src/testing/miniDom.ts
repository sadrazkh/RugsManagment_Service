/**
 * DOM کوچکِ درون‌حافظه برای آزمون کامپوننت‌ها در محیط node (vitest این پروژه DOM شبیه‌سازی‌شده ندارد و نباید فقط برای
 * چند آزمون وابستگی تازه اضافه شود).
 *
 * چه دارد: یک renderer سفارشی Vue (`createRenderer`) روی گره‌های ساده، رویدادها با bubbling (focus/blur نه)، فوکوس با
 * `document.activeElement` و رویدادهای blur → focusout → focus → focusin مثل مرورگر، `querySelector` برای `#id` و
 * `tag[attr="value"]`، و آن‌قدر از `style`/`addEventListener`/`getRootNode` که v-model و v-show خودِ Vue کار کنند.
 * چه ندارد: چیدمان، CSS، اندازه‌ها. آزمونی که به این‌ها نیاز دارد جایش در مرورگر است.
 *
 * فقط از `*.spec.ts` import می‌شود؛ در bundle برنامه نیست.
 */
import { createRenderer, defineComponent, h, markRaw, nextTick, reactive, ref, type Component } from 'vue'

type Listener = (event: FakeEvent) => void

/** رویدادی که handlerهای کامپوننت می‌گیرند (همان عضوهایی که کد ما می‌خواند). */
export interface FakeEvent {
  type: string
  target: FakeNode | null
  currentTarget: FakeNode | null
  relatedTarget: FakeNode | null
  key: string
  altKey: boolean
  shiftKey: boolean
  ctrlKey: boolean
  metaKey: boolean
  defaultPrevented: boolean
  propagationStopped: boolean
  preventDefault(): void
  stopPropagation(): void
  stopImmediatePropagation(): void
}

const NON_BUBBLING = new Set(['focus', 'blur'])

export class FakeNode {
  readonly tag: string
  parentNode: FakeNode | null = null
  children: FakeNode[] = []
  /** متن گرهٔ متنی یا متن مستقیم عنصر (setElementText) */
  text = ''
  attrs: Record<string, unknown> = {}
  style: Record<string, string> = {}
  value = ''
  composing = false
  private listeners = new Map<string, Listener[]>()

  constructor(tag: string) {
    this.tag = tag
    // مثل عنصر واقعی DOM که Vue هرگز reactive نمی‌کند: بی این، گره‌ای که در یک `ref` قالب می‌نشیند (`grid.value`)
    // Proxy می‌شد و `focus()` و `parentNode` از راه آن Proxy می‌دادند، نه خودِ گره؛ پس `contains` و `===` دروغ می‌گفتند
    markRaw(this)
  }

  get id(): string {
    return typeof this.attrs.id === 'string' ? this.attrs.id : ''
  }

  get type(): string {
    return typeof this.attrs.type === 'string' ? this.attrs.type : ''
  }

  get disabled(): boolean {
    const d = this.attrs.disabled
    return d !== undefined && d !== null && d !== false
  }

  get textContent(): string {
    if (this.tag === '#text') return this.text
    if (this.tag === '#comment') return ''
    return this.text + this.children.map((c) => c.textContent).join('')
  }

  get nextSibling(): FakeNode | null {
    const siblings = this.parentNode?.children ?? []
    return siblings[siblings.indexOf(this) + 1] ?? null
  }

  getAttribute(name: string): string | null {
    const v = this.attrs[name]
    return v === undefined || v === null || v === false ? null : String(v)
  }

  addEventListener(type: string, listener: Listener): void {
    const list = this.listeners.get(type) ?? []
    list.push(listener)
    this.listeners.set(type, list)
  }

  removeEventListener(type: string, listener: Listener): void {
    const list = this.listeners.get(type)
    if (list) this.listeners.set(type, list.filter((l) => l !== listener))
  }

  /** فقط برای dispatch */
  listenersFor(type: string): Listener[] {
    return [...(this.listeners.get(type) ?? [])]
  }

  contains(other: unknown): boolean {
    for (let n = other instanceof FakeNode ? other : null; n; n = n.parentNode) if (n === this) return true
    return false
  }

  getRootNode(): FakeNode {
    let n: FakeNode = this
    while (n.parentNode) n = n.parentNode
    return n
  }

  focus(): void {
    if (this.disabled) return
    focusNode(this)
  }

  blur(): void {
    if (fakeDocument.activeElement === this) focusNode(null)
  }

  select(): void {}

  scrollIntoView(): void {}

  /** همهٔ نوادگان به ترتیب سند */
  descendants(): FakeNode[] {
    const out: FakeNode[] = []
    const walk = (n: FakeNode) => {
      for (const c of n.children) {
        out.push(c)
        walk(c)
      }
    }
    walk(this)
    return out
  }

  /** `#id`، `tag`، `[attr]`، `[attr="v"]` و ترکیبشان (بی فاصله: یک عنصر). */
  querySelector(selector: string): FakeNode | null {
    return this.querySelectorAll(selector)[0] ?? null
  }

  querySelectorAll(selector: string): FakeNode[] {
    return this.descendants().filter((n) => matches(n, selector))
  }
}

function matches(node: FakeNode, selector: string): boolean {
  if (node.tag.startsWith('#')) return false
  const m = /^([a-zA-Z][\w-]*)?(?:#([\w-]+))?((?:\[[^\]]+\])*)$/.exec(selector.trim())
  if (!m) throw new Error(`miniDom: انتخاب‌گر پشتیبانی نمی‌شود: ${selector}`)
  const [, tag, id, attrs] = m
  if (tag && node.tag !== tag.toLowerCase()) return false
  if (id && node.id !== id) return false
  for (const a of attrs.matchAll(/\[([\w-]+)(?:="([^"]*)")?\]/g)) {
    const actual = node.getAttribute(a[1])
    if (actual === null) return false
    if (a[2] !== undefined && actual !== a[2]) return false
  }
  return true
}

// ── سند و فوکوس ───────────────────────────────────────────────────────────

class FakeDocumentNode extends FakeNode {
  activeElement: FakeNode | null = null
  body = new FakeNode('body')

  getElementById(id: string): FakeNode | null {
    return this.descendants().find((n) => n.id === id) ?? null
  }
}

export const fakeDocument = new FakeDocumentNode('#document')

function makeEvent(type: string, target: FakeNode | null, init: Partial<FakeEvent> = {}): FakeEvent {
  const event: FakeEvent = {
    type,
    target,
    currentTarget: null,
    relatedTarget: null,
    key: '',
    altKey: false,
    shiftKey: false,
    ctrlKey: false,
    metaKey: false,
    defaultPrevented: false,
    propagationStopped: false,
    preventDefault() {
      event.defaultPrevented = true
    },
    stopPropagation() {
      event.propagationStopped = true
    },
    stopImmediatePropagation() {
      event.propagationStopped = true
    },
    ...init,
  }
  return event
}

/** رویداد را به گره و (جز focus/blur) به نیاکانش می‌دهد؛ رویداد ساخته‌شده برگردانده می‌شود. */
export function dispatch(target: FakeNode, type: string, init: Partial<FakeEvent> = {}): FakeEvent {
  const event = makeEvent(type, target, init)
  const path: FakeNode[] = []
  for (let n: FakeNode | null = target; n; n = n.parentNode) {
    path.push(n)
    if (NON_BUBBLING.has(type)) break
  }
  for (const node of path) {
    event.currentTarget = node
    for (const listener of node.listenersFor(type)) listener(event)
    if (event.propagationStopped) break
  }
  // رویدادهایی که روی document گوش داده شده‌اند (مثل pointerdown بیرون از تقویم)
  if (!event.propagationStopped && !NON_BUBBLING.has(type) && !path.includes(fakeDocument)) {
    event.currentTarget = fakeDocument
    for (const listener of fakeDocument.listenersFor(type)) listener(event)
  }
  return event
}

function focusNode(next: FakeNode | null): void {
  const previous = fakeDocument.activeElement
  if (previous === next) return
  fakeDocument.activeElement = next
  if (previous) {
    dispatch(previous, 'blur', { relatedTarget: next })
    dispatch(previous, 'focusout', { relatedTarget: next })
  }
  if (next) {
    dispatch(next, 'focus', { relatedTarget: previous })
    dispatch(next, 'focusin', { relatedTarget: previous })
  }
}

// ── renderer ──────────────────────────────────────────────────────────────

interface Invoker extends Listener {
  value: unknown
}

type VeiNode = FakeNode & { _vei?: Record<string, Invoker | undefined> }

function eventName(key: string): string {
  // onKeydown → keydown، onUpdate:modelValue در عنصر نمی‌آید
  return key.slice(2).toLowerCase()
}

function patchProp(el: FakeNode, key: string, _prev: unknown, next: unknown): void {
  if (/^on[A-Z]/.test(key)) {
    const node = el as VeiNode
    const invokers = (node._vei ??= {})
    const existing = invokers[key]
    if (next && existing) {
      existing.value = next
    } else if (next) {
      const invoker = ((event: FakeEvent) => {
        const v = invoker.value
        if (Array.isArray(v)) for (const fn of v) (fn as Listener)(event)
        else (v as Listener)(event)
      }) as Invoker
      invoker.value = next
      invokers[key] = invoker
      el.addEventListener(eventName(key), invoker)
    } else if (existing) {
      el.removeEventListener(eventName(key), existing)
      invokers[key] = undefined
    }
    return
  }
  if (key === 'value') {
    el.value = next === null || next === undefined ? '' : String(next)
    return
  }
  if (next === null || next === undefined || next === false) delete el.attrs[key]
  else el.attrs[key] = next
}

const renderer = createRenderer<FakeNode, FakeNode>({
  patchProp,
  insert(child, parent, anchor) {
    if (child.parentNode) child.parentNode.children.splice(child.parentNode.children.indexOf(child), 1)
    const index = anchor ? parent.children.indexOf(anchor) : -1
    if (index >= 0) parent.children.splice(index, 0, child)
    else parent.children.push(child)
    child.parentNode = parent
  },
  remove(child) {
    const parent = child.parentNode
    if (parent) parent.children.splice(parent.children.indexOf(child), 1)
    child.parentNode = null
    if (fakeDocument.activeElement && child.contains(fakeDocument.activeElement)) fakeDocument.activeElement = null
  },
  createElement: (tag) => new FakeNode(tag.toLowerCase()),
  createText(text) {
    const n = new FakeNode('#text')
    n.text = text
    return n
  },
  createComment(text) {
    const n = new FakeNode('#comment')
    n.text = text
    return n
  },
  setText(node, text) {
    node.text = text
  },
  setElementText(el, text) {
    for (const c of el.children) c.parentNode = null
    el.children = []
    el.text = text
  },
  parentNode: (node) => node.parentNode,
  nextSibling: (node) => node.nextSibling,
  querySelector: (selector) => fakeDocument.querySelector(selector),
})

// ── سراسری‌هایی که کد کامپوننت‌ها و Vue می‌خوانند ─────────────────────────

/**
 * `document`، `Node`، `Document`، `ShadowRoot`، `requestAnimationFrame` و `CSS` را با نسخهٔ این DOM جایگزین می‌کند.
 * از آزمون با `vi.stubGlobal` صدا بزنید (این تابع فقط مقدارها را می‌دهد تا آزمون خودش stub را مدیریت کند).
 */
export function domGlobals(): Record<string, unknown> {
  return {
    document: fakeDocument,
    Node: FakeNode,
    Document: FakeDocumentNode,
    ShadowRoot: class {},
    requestAnimationFrame: (cb: (t: number) => void) => {
      cb(0)
      return 0
    },
    CSS: { escape: (s: string) => s },
  }
}

// ── mount ─────────────────────────────────────────────────────────────────

export interface Mounted<E> {
  root: FakeNode
  /** آنچه کامپوننت با defineExpose بیرون داده */
  exposed: E
  /** همهٔ رویدادهای فرستاده‌شده به ترتیب: [نام، آرگومان‌ها] */
  emitted: [string, unknown[]][]
  /** مقدارهای فرستاده‌شدهٔ یک رویداد */
  values(event: string): unknown[]
  /** props تازه (مثل والدی که گزینه‌ها را بعداً می‌آورد) */
  setProps(patch: Record<string, unknown>): Promise<void>
  unmount(): void
}

/**
 * کامپوننت را با props داده‌شده سوار می‌کند. `update:modelValue` مثل v-model والد مقدار را برمی‌گرداند (مگر
 * `vModel: false`)؛ هر رویداد دیگری که نامش در `events` است ثبت می‌شود.
 */
export async function mount<E = Record<string, unknown>>(
  component: Component,
  props: Record<string, unknown>,
  options: { events?: string[]; vModel?: boolean } = {},
): Promise<Mounted<E>> {
  const state = reactive({ ...props })
  const emitted: [string, unknown[]][] = []
  const instance = ref<E | null>(null)
  const events = new Set(['update:modelValue', ...(options.events ?? [])])

  const handlers: Record<string, (...args: unknown[]) => void> = {}
  for (const name of events) {
    const key = `on${name[0].toUpperCase()}${name.slice(1)}`
    handlers[key] = (...args: unknown[]) => {
      emitted.push([name, args])
      if (name === 'update:modelValue' && options.vModel !== false) state.modelValue = args[0]
    }
  }

  const Host = defineComponent({
    setup: () => () => h(component, { ...state, ...handlers, ref: instance }),
  })

  const root = new FakeNode('div')
  fakeDocument.body.children = [root]
  root.parentNode = fakeDocument.body
  fakeDocument.body.parentNode = fakeDocument
  fakeDocument.children = [fakeDocument.body]
  fakeDocument.activeElement = null

  const app = renderer.createApp(Host)
  app.mount(root)
  await nextTick()

  return {
    root,
    get exposed() {
      return instance.value as E
    },
    emitted,
    values: (event) => emitted.filter(([n]) => n === event).map(([, args]) => args[0]),
    async setProps(patch) {
      Object.assign(state, patch)
      await nextTick()
    },
    unmount: () => app.unmount(),
  }
}

// ── کمکی‌های کاربر ────────────────────────────────────────────────────────

/** همان کاری که تایپ کاربر می‌کند: مقدار عوض می‌شود و رویداد input. */
export async function typeInto(input: FakeNode, text: string): Promise<void> {
  input.value = text
  dispatch(input, 'input')
  await nextTick()
}

export async function press(target: FakeNode, key: string, init: Partial<FakeEvent> = {}): Promise<FakeEvent> {
  const event = dispatch(target, 'keydown', { key, ...init })
  await nextTick()
  return event
}

export async function click(target: FakeNode): Promise<void> {
  dispatch(target, 'mousedown')
  target.focus()
  dispatch(target, 'click')
  await nextTick()
}

export async function focus(target: FakeNode | null): Promise<void> {
  if (target) target.focus()
  else fakeDocument.activeElement?.blur()
  await nextTick()
}

/** نخستین عنصر با این انتخاب‌گر؛ نبودنش خطای خوانا. */
export function find(root: FakeNode, selector: string): FakeNode {
  const node = root.querySelector(selector)
  if (!node) throw new Error(`miniDom: «${selector}» پیدا نشد`)
  return node
}

/** دکمه‌ای که متنش این را دارد. */
export function buttonWithText(root: FakeNode, text: string): FakeNode {
  const node = root.querySelectorAll('button').find((b) => b.textContent.includes(text))
  if (!node) throw new Error(`miniDom: دکمهٔ «${text}» پیدا نشد`)
  return node
}
