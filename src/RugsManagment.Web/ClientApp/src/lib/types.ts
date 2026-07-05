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
  stepNameFa: string
  icon: string
  orderIndex: number
  isOptional: boolean
  status: number
  effectiveCost: number
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
  metadataJson?: string
  workflowSteps: RugWorkflowStep[]
  costs: RugCostSummary
}
