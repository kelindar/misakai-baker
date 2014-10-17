// functionobject.cs
//
// Copyright 2010 Microsoft Corporation
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Baker.Text
{
    internal sealed class JsFunctionObject : JsAstNode, IJsNameDeclaration
    {
        private JsBlock m_body;
        private JsAstNodeList m_parameters;

        public JsBlock Body
        {
            get { return m_body; }
            set
            {
                m_body.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_body = value;
                m_body.IfNotNull(n => n.Parent = this);
            }
        }

        public JsAstNodeList ParameterDeclarations
        {
            get { return m_parameters; }
            set
            {
                m_parameters.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_parameters = value;
                m_parameters.IfNotNull(n => n.Parent = this);
            }
        }

        public JsFunctionType FunctionType { get; set; }

        public JsAstNode Initializer { get { return null; } }

        public JsContext NameContext { get { return IdContext; } }

        public bool RenameNotAllowed
        {
            get
            {
                return VariableField == null ? true : !VariableField.CanCrunch;
            }
        }

        public string Name
        {
            get;
            set;
        }

        public string NameGuess
        {
            get;
            set;
        }

        public JsContext IdContext { get; set; }
        public JsContext ParametersContext { get; set; }

        public override bool IsExpression
        {
            get
            {
                // if this is a declaration, then it's not an expression. Otherwise treat it 
                // as if it were an expression.
                return !(FunctionType == JsFunctionType.Declaration);
            }
        }

        // when parsed, this flag indicates that a function declaration is in the
        // proper source-element location
        public bool IsSourceElement
        {
            get;
            set;
        }

        public JsVariableField VariableField { get; set; }
        public int RefCount { get { return (VariableField == null ? 0 : VariableField.RefCount); } }

        public JsFunctionScope FunctionScope { get; set; }

        public override JsActivationObject EnclosingScope
        {
            get
            {
                return FunctionScope;
            }
        }

        public override JsOperatorPrecedence Precedence
        {
            get
            {
                // just assume primary -- should only get called for expressions anyway
                return JsOperatorPrecedence.Primary;
            }
        }

        public JsFunctionObject(JsContext functionContext, JsParser parser)
            : base(functionContext, parser)
        {
        }

        public override void Accept(IJsVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public bool IsReferenced
        {
            get
            {
                // call the checking method with a new empty hashset so it doesn't
                // go in an endless circle
                return SafeIsReferenced(new HashSet<JsFunctionObject>());
            }
        }

        private bool SafeIsReferenced(HashSet<JsFunctionObject> visited)
        {
            // if we've already been here, don't go in a circle
            if (!visited.Contains(this))
            {
                // add us to the visited list
                visited.Add(this);

                if (FunctionType == JsFunctionType.Declaration)
                {
                    // this is a function declaration, so it better have it's variable field set.
                    // if the variable (and therefore the function) is defined in the global scope,
                    // then this function declaration is called by a global function and therefore is
                    // referenced.
                    if (VariableField.OwningScope is JsGlobalScope)
                    {
                        return true;
                    }

                    // not defined in the global scope. Check its references.
                    foreach (var reference in VariableField.References)
                    {
                        var referencingScope = reference.VariableScope;
                        if (referencingScope is JsGlobalScope)
                        {
                            // referenced by a lookup in the global scope -- we're good to go.
                            return true;
                        }
                        else
                        {
                            var functionScope = referencingScope as JsFunctionScope;
                            if (functionScope != null && functionScope.FunctionObject.SafeIsReferenced(visited))
                            {
                                // as soon as we find one that's referenced, we stop
                                return true;
                            }
                        }
                    }
                }
                else
                {
                    // expressions are always referenced
                    return true;
                }
            }

            // if we get here, we aren't referenced by anything that's referenced
            return false;
        }

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(ParameterDeclarations, Body);
            }
        }

        public override bool ReplaceChild(JsAstNode oldNode, JsAstNode newNode)
        {
            if (Body == oldNode)
            {
                Body = ForceToBlock(newNode);
                return true;
            }
            else if (ParameterDeclarations == oldNode)
            {
                var newList = newNode as JsAstNodeList;
                if (newNode == null || newList != null)
                {
                    ParameterDeclarations = newList;
                    return true;
                }
            }

            return false;
        }

        internal override bool RequiresSeparator
        {
            get { return HideFromOutput; }
        }

        internal bool IsArgumentTrimmable(JsVariableField targetArgumentField)
        {
            // walk backward until we either find the given argument field or the
            // first parameter that is referenced. 
            // If we find the argument field, then we can trim it because there are no
            // referenced parameters after it.
            // if we find a referenced argument, then the parameter is not trimmable.
            JsVariableField argumentField = null;
            if (ParameterDeclarations != null)
            {
                for (int index = ParameterDeclarations.Count - 1; index >= 0; --index)
                {
                    // better be a parameter declaration
                    argumentField = (ParameterDeclarations[index] as JsParameterDeclaration).IfNotNull(p => p.VariableField);
                    if (argumentField != null
                        && (argumentField == targetArgumentField || argumentField.IsReferenced))
                    {
                        break;
                    }
                }
            }

            // if the argument field we landed on is the same as the target argument field,
            // then we found the target argument BEFORE we found a referenced parameter. Therefore
            // the argument can be trimmed.
            return (argumentField == targetArgumentField);
        }
    }

    internal enum JsFunctionType
    {
        Declaration,
        Expression,
        Getter,
        Setter
    }
}