using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using XamlX.TypeSystem;

namespace XamlX.Transform
{
    public class CheckingIlEmitter : IXamlXEmitter
    {
        private readonly IXamlXEmitter _inner;
        private Dictionary<IXamlXLabel, string> _unmarkedLabels;
        public int StackBalance { get; set; }
        private bool _hasBranches;
        private bool _paused;

        public CheckingIlEmitter(IXamlXEmitter inner)
        {
            _inner = inner;
            _unmarkedLabels = new Dictionary<IXamlXLabel, string>();

        }

        public IXamlXTypeSystem TypeSystem => _inner.TypeSystem;


        private static readonly Dictionary<StackBehaviour, int> s_balance = new Dictionary<StackBehaviour, int>
        {
            {StackBehaviour.Pop0, 0},
            {StackBehaviour.Pop1, -1},
            {StackBehaviour.Pop1_pop1, -2},
            {StackBehaviour.Popi, -1},
            {StackBehaviour.Popi_pop1, -2},
            {StackBehaviour.Popi_popi, -2},
            {StackBehaviour.Popi_popi8, -2},
            {StackBehaviour.Popi_popi_popi, -3},
            {StackBehaviour.Popi_popr4, -2},
            {StackBehaviour.Popi_popr8, -2},
            {StackBehaviour.Popref, -1},
            {StackBehaviour.Popref_pop1, -2},
            {StackBehaviour.Popref_popi, -2},
            {StackBehaviour.Popref_popi_popi, -3},
            {StackBehaviour.Popref_popi_popi8, -3},
            {StackBehaviour.Popref_popi_popr4, -3},
            {StackBehaviour.Popref_popi_popr8, -3},
            {StackBehaviour.Popref_popi_popref, -3},
            {StackBehaviour.Push0, 0},
            {StackBehaviour.Push1, 1},
            {StackBehaviour.Push1_push1, 2},
            {StackBehaviour.Pushi, 1},
            {StackBehaviour.Pushi8, 1},
            {StackBehaviour.Pushr4, 1},
            {StackBehaviour.Pushr8, 1},
            {StackBehaviour.Pushref, 1},
            {StackBehaviour.Popref_popi_pop1, -3}
        };

        public void Pause()
        {
            _paused = true;
        }

        public void Resume()
        {
            _paused = false;
        }

        public void ExplicitStack(int change)
        {
            if (_paused)
                return;
            StackBalance += change;
            (_inner as CheckingIlEmitter)?.ExplicitStack(change);
        }
        
        void Track(OpCode code, IXamlXMethod method = null, IXamlXConstructor ctor = null)
        {
            if (_paused)
                return;
            if (code.FlowControl == FlowControl.Branch || code.FlowControl == FlowControl.Cond_Branch)
                _hasBranches = true;
            
            if (method != null && (code == OpCodes.Call || code == OpCodes.Callvirt))
            {
                StackBalance -= method.Parameters.Count + (method.IsStatic ? 0 : 1);
                if (method.ReturnType.FullName != "System.Void")
                    StackBalance += 1;
            }
            else if (ctor!= null && (code == OpCodes.Call  || code == OpCodes.Newobj))
            {
                StackBalance -= ctor.Parameters.Count;
                if (code == OpCodes.Newobj)
                    // New pushes a value to the stack
                    StackBalance += 1;
                else
                {
                    if (!ctor.IsStatic)
                        // base ctor pops this from the stack
                        StackBalance -= 1;
                }
            }
            else
            {
                void Balance(StackBehaviour op)
                {
                    if (s_balance.TryGetValue(op, out var balance))
                        StackBalance += balance;
                    else
                        throw new Exception("Don't know how to track stack for " + code);
                }
                Balance(code.StackBehaviourPop);
                Balance(code.StackBehaviourPush);
            }
        }
        
        public IXamlXEmitter Emit(OpCode code)
        {
            Track(code);
            _inner.Emit(code);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, IXamlXField field)
        {
            Track(code);
            _inner.Emit(code, field);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, IXamlXMethod method)
        {
            Track(code, method);
            _inner.Emit(code, method);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, IXamlXConstructor ctor)
        {
            Track(code, null, ctor);
            _inner.Emit(code, ctor);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, string arg)
        {
            Track(code);
            _inner.Emit(code, arg);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, int arg)
        {
            Track(code);
            _inner.Emit(code, arg);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, long arg)
        {
            Track(code);
            _inner.Emit(code, arg);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, IXamlXType type)
        {
            Track(code);
            _inner.Emit(code, type);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, float arg)
        {
            Track(code);
            _inner.Emit(code, arg);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, double arg)
        {
            Track(code);
            _inner.Emit(code, arg);
            return this;
        }

        public IXamlXLocal DefineLocal(IXamlXType type)
        {
            return _inner.DefineLocal(type);
        }

        public IXamlXLabel DefineLabel()
        {
            var label = _inner.DefineLabel();
            _unmarkedLabels.Add(label, null);//, Environment.StackTrace);
            return label;
        }

        public IXamlXEmitter MarkLabel(IXamlXLabel label)
        {
            if (!_unmarkedLabels.Remove(label))
                throw new InvalidOperationException("Attempt to mark undeclared label");
            _inner.MarkLabel(label);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, IXamlXLabel label)
        {
            Track(code);
            _inner.Emit(code, label);
            return this;
        }

        public IXamlXEmitter Emit(OpCode code, IXamlXLocal local)
        {
            Track(code);
            _inner.Emit(code, local);
            return this;
        }

        public void Check(int expectedBalance)
        {
            if (expectedBalance != StackBalance && !_hasBranches)
                throw new InvalidProgramException($"Unbalanced stack, expected {expectedBalance} got {StackBalance}");
            if (_unmarkedLabels.Count != 0)
                throw new InvalidProgramException("Code block has unmarked labels defined at:\n" +
                                                    string.Join("\n", _unmarkedLabels.Values));
        }
    }
}