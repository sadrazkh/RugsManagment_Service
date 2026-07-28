/** شکل دادهٔ DTOهای بک‌اند که جزیره‌ها مصرف می‌کنند (camelCase). */

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
  effectiveCost: number
  completedAt?: string
  /** کاربری که این مرحله را تکمیل کرد */
  completedByName?: string
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
  purchaseCost?: number
  targetSalePrice?: number
  status: number
  imageUrl?: string
  notes?: string
  workflowTemplateId?: string
  batchId?: string
  batchName?: string
  metadataJson?: string
  workflowSteps: RugWorkflowStep[]
  costs: RugCostSummary
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
  totalInvestment: number
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
