using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Windows.Forms;

namespace FormWind.Design;

/// <summary>
/// The preview changes more than the properties <see cref="TailwindProviderDesigner"/> hides from serialization: a button with a
/// background becomes flat (<c>FlatStyle</c>, <c>FlatAppearance</c>), a text box with a painted frame loses its native border,
/// a grid gets its cell styles, a LinkLabel its link colors. Those values were written to Designer.cs and stayed after the
/// classes were removed. While a designer is open, controls get a CodeDOM serializer that wraps their own: while the control is
/// written, the preview is taken off (the values before the classes are back) and applied again afterwards.
/// </summary>
internal static class TwPreviewSerialization
{
    private static readonly object Gate = new object();
    private static readonly List<TailwindProvider> Providers = new List<TailwindProvider>();
    private static TypeDescriptionProvider? _typeProvider;
    // Kontrol türlerinin kendi öznitelikleri (FormWind'in sağlayıcısından önceki sağlayıcı).
    private static TypeDescriptionProvider? _parent;

    /// <summary>A designer for <paramref name="provider"/> was opened.</summary>
    public static void Register(TailwindProvider provider)
    {
        lock (Gate)
        {
            Providers.Add(provider);
            if (_typeProvider != null) return;
            _parent = TypeDescriptor.GetProvider(typeof(Control));
            _typeProvider = new SerializerProvider(_parent);
            TypeDescriptor.AddProvider(_typeProvider, typeof(Control));
        }
    }

    public static void Unregister(TailwindProvider provider)
    {
        lock (Gate)
        {
            Providers.Remove(provider);
            if (Providers.Count > 0 || _typeProvider is null) return;
            TypeDescriptor.RemoveProvider(_typeProvider, typeof(Control));
            _typeProvider = null;
        }
    }

    /// <summary>Takes the preview off <paramref name="value"/> until the returned scope is disposed.</summary>
    internal static IDisposable? Suspend(object value)
    {
        if (value is not Control control) return null;
        List<TailwindProvider> providers;
        lock (Gate) providers = new List<TailwindProvider>(Providers);
        foreach (var provider in providers)
            if (provider.RestoreForSerialization(control)) return new Scope(provider, control);
        return null;
    }

    private sealed class Scope : IDisposable
    {
        private readonly TailwindProvider _provider;
        private readonly Control _control;

        public Scope(TailwindProvider provider, Control control)
        {
            _provider = provider;
            _control = control;
        }

        public void Dispose() => _provider.ReapplyAfterSerialization(_control);
    }

    private static bool IsCodeDom(Attribute attribute) =>
        attribute is DesignerSerializerAttribute { SerializerBaseTypeName: string name } && Type.GetType(name) == typeof(CodeDomSerializer);

    // Control ve alt türlerinin CodeDOM serileştiricisi PreviewSerializer olur; kendi serileştiricileri onun içinde çalışır.
    private sealed class SerializerProvider : TypeDescriptionProvider
    {
        public SerializerProvider(TypeDescriptionProvider parent) : base(parent) { }

        public override ICustomTypeDescriptor? GetTypeDescriptor(Type objectType, object? instance)
        {
            var descriptor = base.GetTypeDescriptor(objectType, instance);
            return descriptor is null ? null : new Descriptor(descriptor);
        }

        private sealed class Descriptor : CustomTypeDescriptor
        {
            public Descriptor(ICustomTypeDescriptor parent) : base(parent) { }

            public override AttributeCollection GetAttributes()
            {
                var attributes = base.GetAttributes();
                for (int i = 0; i < attributes.Count; i++)
                {
                    if (!IsCodeDom(attributes[i])) continue;
                    var result = new Attribute[attributes.Count];
                    attributes.CopyTo(result, 0);
                    result[i] = new DesignerSerializerAttribute(typeof(PreviewSerializer), typeof(CodeDomSerializer));
                    return new AttributeCollection(result);
                }
                return attributes;
            }
        }
    }

    private sealed class PreviewSerializer : CodeDomSerializer
    {
        private static readonly ConcurrentDictionary<Type, CodeDomSerializer?> Inner = new ConcurrentDictionary<Type, CodeDomSerializer?>();

        // Türün kendi serileştiricisi (ControlCodeDomSerializer, DataGridView'inki …); yoksa temel CodeDomSerializer.
        private static CodeDomSerializer InnerFor(IDesignerSerializationManager? manager, Type type) =>
            Inner.GetOrAdd(type, t =>
            {
                var parent = _parent?.GetTypeDescriptor(t);
                if (parent is null) return null;
                foreach (Attribute attribute in parent.GetAttributes())
                {
                    if (!IsCodeDom(attribute) || ((DesignerSerializerAttribute)attribute).SerializerTypeName is not string name) continue;
                    var serializerType = manager?.GetType(name) ?? Type.GetType(name);
                    if (serializerType != null) return (CodeDomSerializer?)Activator.CreateInstance(serializerType, nonPublic: true);
                }
                return null;
            }) ?? Fallback;

        private static readonly CodeDomSerializer Fallback = new BasicSerializer();

        public override object? Serialize(IDesignerSerializationManager manager, object value)
        {
            using (Suspend(value)) return InnerFor(manager, value.GetType()).Serialize(manager, value);
        }

        public override object? SerializeAbsolute(IDesignerSerializationManager manager, object value)
        {
            using (Suspend(value)) return InnerFor(manager, value.GetType()).SerializeAbsolute(manager, value);
        }

        public override System.CodeDom.CodeStatementCollection SerializeMember(IDesignerSerializationManager manager, object owningObject, MemberDescriptor member)
        {
            using (Suspend(owningObject)) return InnerFor(manager, owningObject.GetType()).SerializeMember(manager, owningObject, member);
        }

        public override System.CodeDom.CodeStatementCollection SerializeMemberAbsolute(IDesignerSerializationManager manager, object owningObject, MemberDescriptor member)
        {
            using (Suspend(owningObject)) return InnerFor(manager, owningObject.GetType()).SerializeMemberAbsolute(manager, owningObject, member);
        }

        public override object? Deserialize(IDesignerSerializationManager manager, object codeObject) =>
            InnerFor(manager, typeof(Control)).Deserialize(manager, codeObject);

        public override string? GetTargetComponentName(System.CodeDom.CodeStatement? statement, System.CodeDom.CodeExpression? expression, Type? targetType) =>
            InnerFor(null, targetType ?? typeof(Control)).GetTargetComponentName(statement, expression, targetType);
    }

    private sealed class BasicSerializer : CodeDomSerializer
    {
    }
}
