using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using XamlX.Ast;
using XamlX.Transform.Emitters;
using XamlX.Transform.Transformers;
using XamlX.TypeSystem;

namespace XamlX.Transform
{
    public class XamlXAstTransformationManager
    {
        private readonly XamlXTransformerConfiguration _configuration;
        public List<IXamlXAstTransformer> Transformers { get; } = new List<IXamlXAstTransformer>();
        public List<IXamlXAstNodeEmitter> Emitters { get; } = new List<IXamlXAstNodeEmitter>();
        public XamlXAstTransformationManager(XamlXTransformerConfiguration configuration, bool fillWithDefaults)
        {
            _configuration = configuration;
            if (fillWithDefaults)
            {
                Transformers = new List<IXamlXAstTransformer>
                {
                    new XamlXKnownDirectivesTransformer(),
                    new XamlXIntrinsicsTransformer(),
                    new XamlXXArgumentsTransformer(),
                    new XamlXTypeReferenceResolver(),
                    new XamlXPropertyReferenceResolver(),
                    new XamlXContentTransformer(),
                    new XamlXXamlPropertyValueTransformer()
                };
                Emitters = new List<IXamlXAstNodeEmitter>()
                {
                    new NewObjectEmitter(),
                    new TextNodeEmitter(),
                    new MethodCallEmitter(),
                    new PropertyAssignmentEmitter(),
                    new PropertyValueManipulationEmitter(),
                    new ManipulationGroupEmitter()
                };
            }
        }

        public IXamlXAstNode Transform(IXamlXAstNode root,
            Dictionary<string, string> namespaceAliases, bool strict = true)
        {
            var ctx = new XamlXAstTransformationContext(_configuration, namespaceAliases, strict);

            foreach (var transformer in Transformers)
            {
                root = root.Visit(n => transformer.Transform(ctx, n));
            }

            return root;
        }

        public void Compile(IXamlXAstNode root, IXamlXCodeGen codeGen)
        {
            new XamlXEmitContext(_configuration, Emitters).Emit(root, codeGen);
            codeGen.Generator.Emit(OpCodes.Ret);
        }
    }


    
    public class XamlXAstTransformationContext
    {
        private Dictionary<Type, object> _items = new Dictionary<Type, object>();
        public Dictionary<string, string> NamespaceAliases { get; set; } = new Dictionary<string, string>();      
        public XamlXTransformerConfiguration Configuration { get; }
        public bool StrictMode { get; }

        public IXamlXAstNode Error(IXamlXAstNode node, Exception e)
        {
            if (StrictMode)
                throw e;
            return node;
        }

        public IXamlXAstNode ParseError(string message, IXamlXAstNode node) =>
            Error(node, new XamlXParseException(message, node));
        
        public IXamlXAstNode ParseError(string message, IXamlXAstNode offender, IXamlXAstNode ret) =>
            Error(ret, new XamlXParseException(message, offender));

        public XamlXAstTransformationContext(XamlXTransformerConfiguration configuration,
            Dictionary<string, string> namespaceAliases, bool strictMode = true)
        {
            Configuration = configuration;
            NamespaceAliases = namespaceAliases;
            StrictMode = strictMode;
        }

        public T GetItem<T>() => (T) _items[typeof(T)];
        public void SetItem<T>(T item) => _items[typeof(T)] = item;       
    }


    public class XamlXEmitContext
    {
        private readonly List<object> _emitters;
        public XamlXTransformerConfiguration Configuration { get; }

        public XamlXEmitContext(XamlXTransformerConfiguration configuration, IEnumerable<object> emitters)
        {
            _emitters = emitters.ToList();
            Configuration = configuration;
        }

        public void Emit(IXamlXAstNode value, IXamlXCodeGen codeGen)
        {
            foreach(var e in _emitters)
                if(e is IXamlXAstNodeEmitter ve 
                && ve.Emit(value, this, codeGen))
                    return;
            if (value is IXamlXAstEmitableNode en)
                en.Emit(this, codeGen);
            else
                throw new XamlXLoadException("Unable to find emitter for node type: " + value.GetType().FullName,
                    value);
        }
    }

    public interface IXamlXAstTransformer
    {
        IXamlXAstNode Transform(XamlXAstTransformationContext context, IXamlXAstNode node);
    }
   
    public interface IXamlXAstNodeEmitter
    {
        bool Emit(IXamlXAstNode node, XamlXEmitContext context, IXamlXCodeGen codeGen);
    }

    public interface IXamlXAstEmitableNode
    {
        void Emit(XamlXEmitContext context, IXamlXCodeGen codeGen);
    }
    
}