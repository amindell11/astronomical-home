using System;

namespace Balance
{
    /// <summary>
    /// Marks a serialized field as a balance input, where it is declared. On a number the value is a
    /// balance number. On a reference, or a list of them, the item's numbers continue on the
    /// referenced object. <see cref="StatHash"/> reads marked fields and nothing else. A list's
    /// order and repeats don't move a hash unless the field is marked <see cref="Ordered"/>.
    /// Renaming a marked field, or the asset it sits on, changes every hash that includes it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class StatAttribute : Attribute
    {
        public bool Ordered { get; set; }
    }
}
