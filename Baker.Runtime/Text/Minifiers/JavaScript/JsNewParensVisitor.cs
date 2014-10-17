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
using System.Diagnostics;

namespace Baker.Text
{
    internal class JsNewParensVisitor : IJsVisitor
    {
        private bool m_needsParens;// = false;
        private bool m_outerHasNoArguments;

        public static bool NeedsParens(JsAstNode expression, bool outerHasNoArguments)
        {
            var visitor = new JsNewParensVisitor(outerHasNoArguments);
            expression.Accept(visitor);
            return visitor.m_needsParens;
        }

        private JsNewParensVisitor(bool outerHasNoArguments)
        {
            // save whether or not the outer new-operator has any arguments itself
            m_outerHasNoArguments = outerHasNoArguments;
        }

        #region IVisitor Members

        public void Visit(JsArrayLiteral node)
        {
            // don't recurse; we don't need parens around this
        }

        public void Visit(JsAspNetBlockNode node)
        {
            // don't bother recursing, but let's wrap in parens, just in case 
            // (since we don't know what will be inserted here)
            m_needsParens = true;
        }

        public void Visit(JsBinaryOperator node)
        {
            // lesser precedence than the new operator; use parens
            m_needsParens = true;
        }

        public void Visit(JsCallNode node)
        {
            if (node != null)
            {
                if (node.InBrackets)
                {
                    // if this is a member-bracket operation, then *we* don't need parens, but we shoul
                    // recurse the function in case something in there does
                    node.Function.Accept(this);
                }
                else if (!node.IsConstructor)
                {
                    // we have parens for our call arguments, so we definitely
                    // need to be wrapped and there's no need to recurse
                    m_needsParens = true;
                }
                else
                {
                    // we are a new-operator - if we have any arguments then we're good to go
                    // because those arguments will be associated with us, not the outer new.
                    // but if we don't have any arguments, we might need to be wrapped in parens
                    // so any outer arguments don't get associated with us
                    if (node.Arguments == null || node.Arguments.Count == 0)
                    {
                        m_needsParens = !m_outerHasNoArguments;
                    }
                }
            }
            else
            {
                // shouldn't happen, but we're a call so let's wrap in parens
                m_needsParens = true;
            }
        }

        public void Visit(JsConditionalCompilationComment node)
        {
            if (node != null)
            {
                // recurse the children, but as soon as we get the flag set to true, bail
                foreach (var child in node.Children)
                {
                    child.Accept(this);
                    if (m_needsParens)
                    {
                        break;
                    }
                }
            }
        }

        public void Visit(JsConditionalCompilationElse node)
        {
            // preprocessor nodes are handled outside the real JavaScript parsing
        }

        public void Visit(JsConditionalCompilationElseIf node)
        {
            // preprocessor nodes are handled outside the real JavaScript parsing
        }

        public void Visit(JsConditionalCompilationEnd node)
        {
            // preprocessor nodes are handled outside the real JavaScript parsing
        }

        public void Visit(JsConditionalCompilationIf node)
        {
            // preprocessor nodes are handled outside the real JavaScript parsing
        }

        public void Visit(JsConditionalCompilationOn node)
        {
            // preprocessor nodes are handled outside the real JavaScript parsing
        }

        public void Visit(JsConditionalCompilationSet node)
        {
            // preprocessor nodes are handled outside the real JavaScript parsing
        }

        public void Visit(JsConditional node)
        {
            // lesser precedence than the new operator; use parens
            m_needsParens = true;
        }

        public void Visit(JsConstantWrapper node)
        {
            // we're good
        }

        public void Visit(JsConstantWrapperPP node)
        {
            // we're good
        }

        public void Visit(JsCustomNode node)
        {
            // we're good
        }

        public void Visit(JsFunctionObject node)
        {
            // we're good
        }

        public virtual void Visit(JsGroupingOperator node)
        {
            // definitely does NOT need parens, because we will
            // output parens ourselves. And don't bother recursing.
        }

        public void Visit(JsImportantComment node)
        {
            // don't recurse
        }

        public void Visit(JsLookup node)
        {
            // we're good
        }

        public void Visit(JsMember node)
        {
            // need to recurse the collection
            if (node != null)
            {
                node.Root.Accept(this);
            }
        }

        public void Visit(JsObjectLiteral node)
        {
            // we're good
        }

        public void Visit(JsParameterDeclaration node)
        {
            // we're good
        }

        public void Visit(JsRegExpLiteral node)
        {
            // we're good
        }

        public void Visit(JsThisLiteral node)
        {
            // we're good
        }

        public void Visit(JsUnaryOperator node)
        {
            // lesser precedence than the new operator; use parens
            m_needsParens = true;
        }

        #endregion

        #region nodes we shouldn't hit

        //
        // expression elements we shouldn't get to
        //

        public void Visit(JsAstNodeList node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsGetterSetter node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsObjectLiteralField node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsObjectLiteralProperty node)
        {
            Debug.Fail("shouldn't get here");
        }

        //
        // statements (we should only hit expressions)
        //

        public void Visit(JsBlock node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsBreak node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsConstStatement node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsContinueNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsDebuggerNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsDirectivePrologue node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsDoWhile node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsEmptyStatement node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsForIn node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsForNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsIfNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsLabeledStatement node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsLexicalDeclaration node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsReturnNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsSwitch node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsSwitchCase node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsThrowNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsTryNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsVar node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsVariableDeclaration node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsWhileNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsWithNode node)
        {
            Debug.Fail("shouldn't get here");
        }

        #endregion
    }
}
