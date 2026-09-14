using GoldenPappadam.Domain.Common;

namespace GoldenPappadam.Domain.Inventory;

/// <summary>
/// How a product's stock is counted: kg, pieces, packets, boxes.
/// A table rather than an enum, so a new unit does not need a code change.
/// There is no conversion between units: conversion happens only when packing,
/// through Product.SourceQuantityPerPack.
/// </summary>
public class UnitOfMeasure : AuditableEntity
{
    public required string Code { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;
}
