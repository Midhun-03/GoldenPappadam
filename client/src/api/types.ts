export type ProductKind = 'Loose' | 'Packed'

export type StockMovementType =
  | 'Opening'
  | 'Production'
  | 'Packing'
  | 'Sale'
  | 'SaleReversal'
  | 'Damage'
  | 'Adjustment'

export type CurrentUser = {
  id: string
  email: string
  fullName: string
}

export type Category = {
  id: string
  name: string
  isActive: boolean
}

export type Unit = {
  id: string
  code: string
  name: string
  isActive: boolean
}

export type Product = {
  id: string
  productCode: string
  name: string
  categoryId: string
  categoryName: string
  kind: ProductKind
  unitOfMeasureId: string
  unitCode: string
  sourceProductId: string | null
  sourceProductName: string | null
  sourceQuantityPerPack: number | null
  sellingPrice: number | null
  lowStockThreshold: number | null
  isActive: boolean
}

export type SaveProduct = {
  productCode: string
  name: string
  categoryId: string
  kind: ProductKind
  unitOfMeasureId: string
  sourceProductId: string | null
  sourceQuantityPerPack: number | null
  sellingPrice: number | null
  lowStockThreshold: number | null
}

export type StockOnHand = {
  productId: string
  productCode: string
  name: string
  kind: ProductKind
  unitCode: string
  quantityOnHand: number
  lowStockThreshold: number | null
  isLowStock: boolean
  isActive: boolean
}

export type StockMovement = {
  id: string
  occurredAt: string
  movementType: StockMovementType
  quantity: number
  runningBalance: number
  referenceType: string | null
  referenceId: string | null
  notes: string | null
}

export type StockEntryResponse = {
  movementId: string
  productId: string
  quantityOnHand: number
  warning: string | null
}

export type PackingResponse = {
  packingEntryId: string
  packedProductId: string
  packsProduced: number
  packedQuantityOnHand: number
  sourceProductId: string
  sourceQuantityUsed: number
  sourceQuantityOnHand: number
  warning: string | null
}

export type PackingEntry = {
  id: string
  occurredAt: string
  packedProductId: string
  packedProductName: string
  packsProduced: number
  sourceProductId: string
  sourceProductName: string
  sourceQuantityUsed: number
  notes: string | null
}
