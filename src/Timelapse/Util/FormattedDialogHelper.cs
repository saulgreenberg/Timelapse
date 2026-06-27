using System;
using System.Reflection;
using TimelapseWpf.Toolkit;

namespace Timelapse.Util
{
    /// <summary>
    /// Helper class for working with FormattedDialog and FormattedMessageContent from TimelapseWpf.Toolkit
    /// Provides static reference resolution for Timelapse-specific constants
    /// </summary>
    public static class FormattedDialogHelper
    {
        /// <summary>
        /// Resolves static references in formatted text
        /// Supports syntax like {x:Static constant:ExternalLinks.TimelapseGuideReference}
        /// Maps namespace prefixes to actual Timelapse namespaces
        /// </summary>
        public static string ResolveStaticReference(string namespacePrefix, string className, string memberName)
        {
            // Map namespace prefixes to actual namespace paths in Timelapse
            var fullTypeName = namespacePrefix switch
            {
                "constant" => $"Timelapse.Constant.{className}",  // e.g., constant:ExternalLinks.TimelapseGuideReference
                "control" => $"Timelapse.Constant.{className}",   // e.g., control:Control.DefaultControlWidth
                _ => null
            };

            if (string.IsNullOrEmpty(fullTypeName))
            {
                // Unknown namespace prefix - return original text
                return $"{{x:Static {namespacePrefix}:{className}.{memberName}}}";
            }

            try
            {
                // Use reflection to get the type
                // Try searching in the executing assembly
                var type = Type.GetType(fullTypeName) ?? Assembly.GetExecutingAssembly().GetType(fullTypeName);

                if (type == null)
                {
                    // Type not found - return original text
                    return $"{{x:Static {namespacePrefix}:{className}.{memberName}}}";
                }

                // Try to get the field value (for const fields and static readonly fields)
                var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null)?.ToString() ?? "";
                }

                // Try to get the property value (for static properties including interpolated strings)
                var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
                if (property != null)
                {
                    return property.GetValue(null)?.ToString() ?? "";
                }

                // Member not found - return original text
                return $"{{x:Static {namespacePrefix}:{className}.{memberName}}}";
            }
            catch (Exception)
            {
                // On error, return original text unchanged
                return $"{{x:Static {namespacePrefix}:{className}.{memberName}}}";
            }
        }

        /// <summary>
        /// Sets up the StaticReferenceResolver on a FormattedDialog's MessageContent
        /// Call this after creating a FormattedDialog but before BuildAndShowDialog()
        /// </summary>
        public static void SetupStaticReferenceResolver(FormattedDialog dialog)
        {
            if (dialog?.FindName("MessageContent") is FormattedMessageContent messageContent)
            {
                messageContent.StaticReferenceResolver = ResolveStaticReference;
            }
        }

        /// <summary>
        /// Sets up the StaticReferenceResolver directly on a FormattedMessageContent
        /// </summary>
        public static void SetupStaticReferenceResolver(FormattedMessageContent messageContent)
        {
            if (messageContent != null)
            {
                messageContent.StaticReferenceResolver = ResolveStaticReference;
            }
        }
    }
}
