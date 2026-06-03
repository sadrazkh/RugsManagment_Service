/**
 * شکل داده‌هایی که API برمی‌گرداند — باید با DTOهای C# هم‌خوان باشد.
 */

export type UserRole = 'SystemAdmin' | 'TenantAdmin' | 'Operator'

export interface User {
  id: string
  email: string
  fullName: string
  role: UserRole
  tenantId?: string
  tenantName?: string
}

export interface AuthResponse {
  token: string
  expiresAt: string
  user: User
}

export type RugStatus = 'Draft' | 'InProgress' | 'ReadyForSale' | 'Sold' | 'Archived'
export type WorkflowStepStatus = 'Pending' | 'InProgress' | 'Completed' | 'Skipped' | 'Cancelled'

export type StepPricingModel =
  | 'Fixed'
  | 'PerSquareMeter'
  | 'PerLength'
  | 'PerWidth'
  | 'Combined'

export interface CombinedPricingItem {
  model: Exclude<StepPricingModel, 'Combined'>
  rate?: number
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
  stepCode: string
  stepNameFa: string
  stepNameEn: string
  icon: string
  orderIndex: number
  isOptional: boolean
  status: WorkflowStepStatus
  serviceProviderId?: string
  serviceProviderName?: string
  startedAt?: string
  completedAt?: string
  effectiveCost: number
  calculatedCost?: number
  appliedPricingModel?: StepPricingModel
  appliedUnitRate?: number
  pricingConfigJson?: string
  fieldValuesJson?: string
  notes?: string
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
  status: RugStatus
  imageUrl?: string
  notes?: string
  workflowTemplateId?: string
  batchId?: string
  batchName?: string
  currentStepNameFa?: string
  currentStepIndex: number
  workflowSteps: RugWorkflowStep[]
  costs: RugCostSummary
}

export interface AdvanceRugStepPayload {
  serviceProviderId?: string
  manualCostOverride?: number
  pricingModel?: StepPricingModel
  unitRate?: number
  pricingConfigJson?: string
  fieldValuesJson?: string
  notes?: string
  markCompleted?: boolean
}

export interface RugBatch {
  id: string
  name: string
  description?: string
  receivedAt?: string
  rugCount: number
  currentStepSummary?: string
}

export interface BulkOperationResult {
  successCount: number
  failedCount: number
  errors: { rugId: string; message: string }[]
}

export interface ProcessStepType {
  id: string
  code: string
  nameFa: string
  nameEn: string
  icon: string
  sortOrder: number
  defaultPricingModel: string
  defaultUnitRate: number
}

export interface WorkflowTemplateStep {
  id: string
  processStepTypeId: string
  stepCode: string
  stepNameFa: string
  orderIndex: number
  isOptional: boolean
  defaultServiceProviderId?: string
}

export interface WorkflowTemplate {
  id: string
  name: string
  description?: string
  isDefault: boolean
  isActive: boolean
  steps: WorkflowTemplateStep[]
}

export interface ServiceProvider {
  id: string
  name: string
  specialty?: string
  phone?: string
}

export interface DashboardStats {
  totalRugs: number
  inProgress: number
  readyForSale: number
  sold: number
  totalInvestment: number
  pipelineValue: number
  recentRugs: { id: string; sku: string; title?: string; status: string; currentStepName?: string; totalInvestment: number }[]
  stepDistribution: { stepName: string; count: number }[]
}

export interface Tenant {
  id: string
  name: string
  slug: string
  isActive: boolean
  contactPhone?: string
  contactEmail?: string
}
