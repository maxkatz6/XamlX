using System;
using System.Collections.Generic;
using XamlX.TypeSystem;

namespace XamlX.Transform
{
    public class XamlXLanguageTypeMappings
    {
        public XamlXLanguageTypeMappings(IXamlXTypeSystem typeSystem)
        {
            ServiceProvider = typeSystem.GetType("System.IServiceProvider");
            TypeDescriptorContext = typeSystem.GetType("System.ComponentModel.ITypeDescriptorContext");
            SupportInitialize = typeSystem.GetType("System.ComponentModel.ISupportInitialize");
            var tconv = typeSystem.GetType("System.ComponentModel.TypeConverterAttribute");
            if (tconv != null)
                TypeConverterAttributes.Add(tconv);
        }

        public List<IXamlXType> XmlnsAttributes { get; set; } = new List<IXamlXType>();
        public List<IXamlXType> UsableDuringInitializationAttributes { get; set; } = new List<IXamlXType>();
        public List<IXamlXType> ContentAttributes { get; set; } = new List<IXamlXType>();
        public List<IXamlXType> TypeConverterAttributes { get; set; } = new List<IXamlXType>();
        public IXamlXType ServiceProvider { get; set; }
        public IXamlXType TypeDescriptorContext { get; set; }
        public IXamlXType SupportInitialize { get; set; }
        public IXamlXType ProvideValueTarget { get; set; }
        public IXamlXType RootObjectProvider { get; set; }
        public IXamlXType ParentStackProvider { get; set; }
        public IXamlXType XmlNamespaceInfoProvider { get; set; }
        public IXamlXType UriContextProvider { get; set; }
        
        public IXamlXCustomAttributeResolver CustomAttributeResolver { get; set; }
        /// <summary>
        /// Expected signature:
        /// static void ApplyNonMatchingMarkupExtension(object target, object property, IServiceProvider prov, object value)
        /// </summary>
        public IXamlXMethod MarkupExtensionCustomResultHandler { get; set; }
        public List<IXamlXType> MarkupExtensionCustomResultTypes { get; set; } = new List<IXamlXType>();
        public Func<IXamlXProperty, IXamlXType, bool> ShouldIgnoreMarkupExtensionCustomResultForProperty { get; set; }
        
        /// <summary>
        /// Expected signature:
        /// static IServiceProvider InnerServiceProviderFactory(IServiceProvider self);
        /// </summary>
        public IXamlXMethod InnerServiceProviderFactoryMethod { get; set; }
        /// <summary>
        /// static Func&lt;IServiceProvider, object&gt; DeferredTransformationFactory(Func&lt;IServiceProvider, object&gt; builder, IServiceProvider provider);
        /// </summary>
        public IXamlXMethod DeferredContentExecutorCustomization { get; set; }
        public List<IXamlXType> DeferredContentPropertyAttributes { get; set; } = new List<IXamlXType>();
    }

    public interface IXamlXCustomAttributeResolver
    {
        IXamlXCustomAttribute GetCustomAttribute(IXamlXType type, IXamlXType attributeType);
        IXamlXCustomAttribute GetCustomAttribute(IXamlXProperty property, IXamlXType attributeType);
    }
}