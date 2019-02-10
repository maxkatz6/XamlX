using System.Collections.Generic;
using System.Linq;
using XamlX.Ast;
using XamlX.TypeSystem;

namespace XamlX.Transform.Transformers
{
    public class XamlPropertyReferenceResolver : IXamlAstTransformer
    {
        public IXamlAstNode Transform(XamlAstTransformationContext context, IXamlAstNode node)
        {
            if (node is XamlAstNamePropertyReference prop)
            {
                if (!(prop.DeclaringType is XamlAstClrTypeReference declaringRef))
                {
                    if (context.StrictMode)
                        throw new XamlParseException(
                            $"Unable to resolve property {prop.Name} on {prop.DeclaringType}", node);
                    else
                        return node;
                }

                if (!(prop.TargetType is XamlAstClrTypeReference targetRef))
                {
                    if (context.StrictMode)
                        throw new XamlParseException($"Unable to resolve property on {prop.DeclaringType}", node);
                    else
                        return node;
                }

                var targetType = targetRef.Type;
                var declaringType = declaringRef.Type;

                // Can set normal properties of ancestor types and self
                if (declaringType.IsAssignableFrom(targetType))
                {
                    var found = declaringType.Properties.FirstOrDefault(p =>
                        p.Name == prop.Name
                        && ((p.Getter != null && !p.Getter.IsStatic && p.Getter.Parameters.Count == 0)
                            || p.Setter != null && !p.Setter.IsStatic && p.Setter.Parameters.Count == 1));
                    if (found != null)
                        return new XamlAstClrPropertyReference(prop, found);
                }

                // Look for attached properties on declaring type
                IXamlMethod setter = null, getter = null;
                var setterName = "Set" + prop.Name;
                var getterName = "Get" + prop.Name;
                foreach (var m in declaringType.Methods)
                {
                    if (m.IsPublic && m.IsStatic)
                    {
                        if (m.Name == getterName && m.Parameters.Count == 1 &&
                            m.Parameters[0].IsAssignableFrom(targetType))
                            getter = m;

                        if (m.Name == setterName && m.Parameters.Count == 2 &&
                            m.Parameters[0].IsAssignableFrom(targetType))
                            setter = m;
                    }
                }

                if (setter == null && getter == null)
                {
                    if (context.StrictMode)
                        throw new XamlParseException(
                            $"Unable to resolve suitable regular or attached property {prop.Name} on type {declaringType.GetFqn()}",
                            node);
                    return null;
                }

                return new XamlAstClrPropertyReference(prop, new XamlXAstAttachedProperty(prop.Name, setter, getter));
            }

            return node;
        }
    }

    class XamlXAstAttachedProperty : IXamlProperty
    {
        public bool Equals(IXamlProperty other)
        {
            if (other == null)
                return false;
            return other.Name == Name
                   && other.Getter.Equals(Getter)
                   && other.Setter.Equals(Setter);
        }

        public string Name { get; }
        public IXamlType PropertyType { get; }
        public IXamlMethod Setter { get; }
        public IXamlMethod Getter { get; }
        public IReadOnlyList<IXamlCustomAttribute> CustomAttributes { get; set; } = new IXamlCustomAttribute[0];

        public XamlXAstAttachedProperty(string name, IXamlMethod setter, IXamlMethod getter)
        {
            Name = name;
            Setter = setter;
            Getter = getter;
            PropertyType = getter != null ? getter.ReturnType : setter.Parameters[1];
        }
    }

}