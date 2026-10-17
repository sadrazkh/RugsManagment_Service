/**
 * شکل دادهٔ DTOهای بک‌اند که جزیره‌های قدیمی مصرف می‌کنند (camelCase).
 *
 * این همان محتوای `lib/types.ts` قبلی است که این‌جا منتقل شد؛ `lib/types/index.ts` فقط همین را دوباره
 * export می‌کند تا importهای `@/lib/types` بی‌تغییر کار کنند. کد تازه نوعش را از
 * `@/lib/types/{feature}` می‌گیرد، نه از این فایل.
 *
 * پنهان‌کاری مبلغ (از فاز سکو): برای کاربری که Business نیست (انباردار)، سرور عضوهای پولی را null
 * می‌فرستد — `Rug.costs`، `Rug.purchaseCost`، `Rug.targetSalePrice`، `RugListItem.totalInvestment` و
 * عضوهای پولی هر مرحله (`effectiveCost`، `calculatedCost`، `appliedUnitRate`، `manualCostOverride`،
 * `adjustment`، `pricingConfigJson`). نوع این عضوها برای همین `| null` است؛ پیش از نمایش مبلغ
 * `moneyVisible()` از `@/lib/context` را هم بپرسید.
 */

export interface WorkflowTemplateStep {
  id: string
  processStepTypeId: string
  stepCode: string
  stepNameFa: string
  orderIndex: number
  isOptional: boolean
}
export interface WorkflowTemplate {
  id: string
  name: string
  description?: string
  isDefault: boolean
  isActive: boolean
  steps: WorkflowTemplateStep[]
}

export type CustomFieldType = 0 | 1 | 2 | 3 | 4 // Text, Number, Date, Select, Boolean
export interface CustomFieldDefinition {
  id: string
  key: string
  label: string
  fieldType: CustomFieldType
  optionsJson?: string
  isRequired: boolean
  sortOrder: number
  isActive: boolean
}

export interface RugCostSummary {
  totalProcessCost: number
  purchaseCost: number
  totalInvestment: number
  targetSalePrice?: number
  estimatedMargin?: number
}

export interface RugWorkflowStep {
  id: string
  processStepTypeId: string
  stepNameFa: string
  icon: string
  orderIndex: number
  isOptional: boolean
  status: number
  /** هزینهٔ مؤثر مرحله؛ برای غیر Business null است. */
  effectiveCost: number | null
  completedAt?: string
  /** کاربری که این مرحله را تکمیل کرد */
  completedByName?: string
  /** مقادیر فرم داینامیک این مرحله */
  fieldValuesJson?: string
  /**
   * مبلغ دستیِ ذخیره‌شده (null یعنی ندارد). بر هر روش و نرخی مقدم است؛ ویرایشگر هزینه با آن حالت «دستی»
   * را از پیش پر می‌کند و اگر کاربر از حالت دستی بیرون بیاید clearManualOverride می‌فرستد.
   */
  manualCostOverride?: number | null
  /** هزینهٔ محاسبه‌شده (null برای غیر Business) */
  calculatedCost?: number | null
  /** روش قیمت‌گذاری اعمال‌شده، به‌صورت نام (مثل «PerSquareMeter») */
  appliedPricingModel?: string | null
  /** نرخ اعمال‌شده (null برای غیر Business) */
  appliedUnitRate?: number | null
  /** بندهای روش ترکیبی (null برای غیر Business) */
  pricingConfigJson?: string | null
  /** تعدیل؛ منفی یعنی تخفیف (null برای غیر Business) */
  adjustment?: number | null
}

export interface Rug {
  id: string
  sku: string
  title?: string
  origin?: string
  pattern?: string
  material?: string
  knotDensity?: number
  widthMeters: number
  lengthMeters: number
  areaSquareMeters: number
  /** قیمت خرید؛ برای غیر Business null است. */
  purchaseCost?: number | null
  /** قیمت هدف فروش؛ برای غیر Business null است. */
  targetSalePrice?: number | null
  status: number
  imageUrl?: string
  notes?: string
  workflowTemplateId?: string
  batchId?: string
  batchName?: string
  metadataJson?: string
  workflowSteps: RugWorkflowStep[]
  /** خلاصهٔ هزینه؛ برای غیر Business null. */
  costs: RugCostSummary | null
}

/**
 * ردیف سبک فهرست فرش‌ها — معادل RugListItemDto در سرور.
 * برخلاف Rug، مراحل را همراه ندارد؛ فقط چیزی که فهرست نشان می‌دهد.
 */
export interface RugListItem {
  id: string
  sku: string
  title?: string
  origin?: string
  pattern?: string
  widthMeters: number
  lengthMeters: number
  areaSquareMeters: number
  status: number
  imageUrl?: string
  batchId?: string
  batchName?: string
  currentStepNameFa?: string
  activeStepId?: string
  /** سرمایه‌گذاری کل؛ برای غیر Business null. */
  totalInvestment: number | null
  completedStepCount: number
  totalStepCount: number
  createdAt: string
}

/** یک صفحه از نتایج — معادل PagedResult<T> در سرور. */
export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}
