// FinalPassVisitor.cs
//
// Copyright 2011 Microsoft Corporation
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
using System.Diagnostics;
using System.Text;

namespace Baker.Text
{
    internal class JsStatementStartVisitor : IJsVisitor
    {
        #region private fields 

        /// <summary>
        /// This is the flag that we are going to return to indicate whether or not
        /// the statement start is safe (true) or requires parens (false)
        /// </summary>
        private bool m_isSafe;

        #endregion

        public JsStatementStartVisitor()
        {
        }

        public bool IsSafe(JsAstNode node)
        {
            // assume it is unless preven otherwise
            m_isSafe = true;
            node.IfNotNull(n => n.Accept(this));
            return m_isSafe;
        }

        #region IVisitor that may recurse

        public void Visit(JsBinaryOperator node)
        {
            // if there's a left-hand operand, recurse into it
            if (node != null && node.Operand1 != null)
            {
                node.Operand1.Accept(this);
            }
        }

        public void Visit(JsCallNode node)
        {
            // if there's a function node, recurse into it
            if (node != null && node.Function != null)
            {
                node.Function.Accept(this);
            }
        }

        public void Visit(JsConditional node)
        {
            // if there's a condition node, recurse into it
            if (node != null && node.Condition != null)
            {
                node.Condition.Accept(this);
            }
        }

        public void Visit(JsMember node)
        {
            // if there's a root node, recurse into it
            if (node != null && node.Root != null)
            {
                node.Root.Accept(this);
            }
        }

        public void Visit(JsUnaryOperator node)
        {
            // if this is a postfix operator and there is an operand, recurse into it
            if (node != null && node.IsPostfix && node.Operand != null)
            {
                node.Operand.Accept(this);
            }
        }

        #endregion

        #region IVisitor that return false

        public void Visit(JsCustomNode node)
        {
            // we don't know, so assume it's not safe and bail.
            m_isSafe = false;
        }

        public void Visit(JsFunctionObject node)
        {
            // this shouldn't be called for anything but a function expression,
            // which is definitely NOT safe to start a statement off because it would
            // then be interpreted as a function *declaration*.
            Debug.Assert(node == null || node.FunctionType == JsFunctionType.Expression);
            m_isSafe = false;
        }

        public void Visit(JsObjectLiteral node)
        {
            // NOT safe -- if it starts a statement off, it would be interpreted as a block,
            // not an object literal.
            m_isSafe = false;
        }

        #endregion

        #region IVisitor nodes that return false

        public void Visit(JsArrayLiteral node)
        {
            // starts with a '[', so we don't care
        }

        public void Visit(JsAspNetBlockNode node)
        {
            // starts with a '<%', so we don't care
        }

        public void Visit(JsBlock node)
        {
            // if we got here, then the block is at the statement level, which means it's
            // a nested block that hasn't been optimized out. 
            // Therefore it starts with a '{' and we don't care.
        }

        public void Visit(JsBreak node)
        {
            // starts with a 'break', so we don't care
        }

        public void Visit(JsConditionalCompilationComment node)
        {
            // starts with a '/*@' or '//@', so we don't care
        }

        public void Visit(JsConditionalCompilationElse node)
        {
            // starts with a '@else', so we don't care
        }

        public void Visit(JsConditionalCompilationElseIf node)
        {
            // starts with a '@elif', so we don't care
        }

        public void Visit(JsConditionalCompilationEnd node)
        {
            // starts with a '@end', so we don't care
        }

        public void Visit(JsConditionalCompilationIf node)
        {
            // starts with a '@if', so we don't care
        }

        public void Visit(JsConditionalCompilationOn node)
        {
            // starts with a '@cc_on', so we don't care
        }

        public void Visit(JsConditionalCompilationSet node)
        {
            // starts with a '@set', so we don't care
        }

        public void Visit(JsConstantWrapper node)
        {
            // it's a constant, so we don't care
        }

        public void Visit(JsConstantWrapperPP node)
        {
            // it's a constant, so we don't care
        }

        public void Visit(JsConstStatement node)
        {
            // starts with a 'const', so we don't care
        }

        public void Visit(JsContinueNode node)
        {
            // starts with a 'continue', so we don't care
        }

        public void Visit(JsDebuggerNode node)
        {
            // starts with a 'debugger', so we don't care
        }

        public void Visit(JsDirectivePrologue node)
        {
            // just a string, so we don't care
        }

        public void Visit(JsDoWhile node)
        {
            // starts with a 'do', so we don't care
        }

        public void Visit(JsEmptyStatement node)
        {
            // empty statement, so we don't care
        }

        public void Visit(JsForIn node)
        {
            // starts with a 'for', so we don't care
        }

        public void Visit(JsForNode node)
        {
            // starts with a 'for', so we don't care
        }

        public void Visit(JsGetterSetter node)
        {
            // starts with a 'get' or a 'set', so we don't care
        }

        public void Visit(JsGroupingOperator node)
        {
            // starts with a '(', so we don't care
        }

        public void Visit(JsIfNode node)
        {
            // starts with an 'if', so we don't care
        }

        public void Visit(JsImportantComment node)
        {
            // comment, so we need to keep going
        }

        public void Visit(JsLabeledStatement node)
        {
            // starts with a label identifier, so we don't care
        }

        public void Visit(JsLexicalDeclaration node)
        {
            // starts with a 'let', so we don't care
        }

        public void Visit(JsLookup node)
        {
            // lookup identifier, so we don't care
        }

        public void Visit(JsRegExpLiteral node)
        {
            // regexp literal, so we don't care
        }

        public void Visit(JsReturnNode node)
        {
            // starts with 'return', so we don't care
        }

        public void Visit(JsSwitch node)
        {
            // starts with 'switch', so we don't care
        }

        public void Visit(JsThisLiteral node)
        {
            // this literal, so we don't care
        }

        public void Visit(JsThrowNode node)
        {
            // starts with 'throw', so we don't care
        }

        public void Visit(JsTryNode node)
        {
            // starts with 'try', so we don't care
        }

        public void Visit(JsVar node)
        {
            // starts with 'var', so we don't care
        }

        public void Visit(JsWhileNode node)
        {
            // starts with 'while', so we don't care
        }

        public void Visit(JsWithNode node)
        {
            // starts with 'with', so we don't care
        }

        #endregion

        #region IVisitor nodes we shouldn't hit (because their parents don't recurse)

        public void Visit(JsAstNodeList node)
        {
            // shoudn't get here
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsObjectLiteralField node)
        {
            // shoudn't get here
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsObjectLiteralProperty node)
        {
            // shoudn't get here
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsParameterDeclaration node)
        {
            // shoudn't get here
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsSwitchCase node)
        {
            // shoudn't get here
            Debug.Fail("shouldn't get here");
        }

        public void Visit(JsVariableDeclaration node)
        {
            // shoudn't get here
            Debug.Fail("shouldn't get here");
        }

        #endregion
    }
}
