using System;

// ReSharper disable once CheckNamespace
namespace JetBrains.Annotations
{
    // Embedded copy of MustUseReturnValueAttribute so ReSharper can warn when
    // SqlOperationResult is discarded without checking .Success, without requiring
    // the JetBrains.Annotations NuGet package.
    // If that package is added later, delete this file — the package's version takes over.
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Parameter |
        AttributeTargets.Property | AttributeTargets.Delegate | AttributeTargets.Field)]
    internal sealed class MustUseReturnValueAttribute : Attribute
    {
        internal MustUseReturnValueAttribute() { }
        internal MustUseReturnValueAttribute(string justification) { Justification = justification; }
        internal string Justification { get; }
    }
}
