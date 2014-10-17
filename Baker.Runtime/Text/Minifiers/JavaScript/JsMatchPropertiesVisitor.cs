// MatchPropertiesVisitor.cs
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
using System.Text;

namespace Baker.Text
{
    /// <summary>
    /// This visitor has a Match method that takes a node and an string representing an identifier list separated by periods: IDENT(.IDENT)*
    /// </summary>
    internal class JsMatchPropertiesVisitor : IJsVisitor
    {
        private string[] m_parts;
        private bool m_isMatch;
        private int m_index;

        public JsMatchPropertiesVisitor()
        {
        }

        public bool Match(JsAstNode node, string identifiers)
        {
            // set the match to false
            m_isMatch = false;

            // identifiers cannot be null or blank and must match: IDENT(.IDENT)*
            // since for JS there has to be at least a global object, the dot must be AFTER the first character.
            if (node != null && !string.IsNullOrEmpty(identifiers))
            {
                // get all the parts
                var parts = identifiers.Split('.');

                // each part must be a valid JavaScript identifier. Assume everything is valid
                // unless at least one is invalid -- then forget it
                var isValid = true;
                foreach (var part in parts)
                {
                    if (!JsScanner.IsValidIdentifier(part))
                    {
                        isValid = false;
                        break;
                    }
                }

                // must be valid to continue
                if (isValid)
                {
                    // save the parts and start the index on the last one, since we'll be walking backwards
                    m_parts = parts;
                    m_index = parts.Length - 1;

                    node.Accept(this);
                }
            }

            return m_isMatch;
        }

        public void Visit(JsCallNode node)
        {
            // only interested if the index is greater than zero, since the zero-index
            // needs to be a lookup. Also needs to be a brackets-call, and there needs to
            // be a single argument.
            if (node != null
                && m_index > 0
                && node.InBrackets
                && node.Arguments != null
                && node.Arguments.Count == 1)
            {
                // better be a constant wrapper, too
                var constantWrapper = node.Arguments[0] as JsConstantWrapper;
                if (constantWrapper != null && constantWrapper.PrimitiveType == JsPrimitiveType.String)
                {
                    // check the value of the constant wrapper against the current part
                    if (string.CompareOrdinal(constantWrapper.Value.ToString(), m_parts[m_index--]) == 0)
                    {
                        // match! recurse the function after decrementing the index
                        node.Function.Accept(this);
                    }
                }
            }
        }

        public void Visit(JsMember node)
        {
            // only interested if the index is greater than zero, since the zero-index
            // needs to be a lookup.
            if (node != null && m_index > 0)
            {
                // check the Name property against the current part
                if (string.CompareOrdinal(node.Name, m_parts[m_index--]) == 0)
                {
                    // match! recurse the root after decrementing the index
                    node.Root.Accept(this);
                }
            }
        }

        public void Visit(JsLookup node)
        {
            // we are only a match if we are looking for the first part
            if (node != null && m_index == 0)
            {
                // see if the name matches; and if there is a field, it should be a global
                if (string.CompareOrdinal(node.Name, m_parts[0]) == 0
                    && (node.VariableField == null || node.VariableField.FieldType == JsFieldType.UndefinedGlobal 
                    || node.VariableField.FieldType == JsFieldType.Global))
                {
                    // match!
                    m_isMatch = true;
                }
            }
        }

        public virtual void Visit(JsGroupingOperator node)
        {
            if (node != null && node.Operand != null)
            {
                // just totally ignore any parentheses
                node.Operand.Accept(this);
            }
        }

        #region IVisitor Members

        public void Visit(JsArrayLiteral node)
        {
            // not applicable; terminate
        }

        public void Visit(JsAspNetBlockNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsAstNodeList node)
        {
            // not applicable; terminate
        }

        public void Visit(JsBinaryOperator node)
        {
            // not applicable; terminate
        }

        public void Visit(JsBlock node)
        {
            // not applicable; terminate
        }

        public void Visit(JsBreak node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationComment node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationElse node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationElseIf node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationEnd node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationIf node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationOn node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditionalCompilationSet node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConditional node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConstantWrapper node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConstantWrapperPP node)
        {
            // not applicable; terminate
        }

        public void Visit(JsConstStatement node)
        {
            // not applicable; terminate
        }

        public void Visit(JsContinueNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsCustomNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsDebuggerNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsDirectivePrologue node)
        {
            // not applicable; terminate
        }

        public void Visit(JsDoWhile node)
        {
            // not applicable; terminate
        }

        public void Visit(JsEmptyStatement node)
        {
            // not applicable; terminate
        }

        public void Visit(JsForIn node)
        {
            // not applicable; terminate
        }

        public void Visit(JsForNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsFunctionObject node)
        {
            // not applicable; terminate
        }

        public void Visit(JsGetterSetter node)
        {
            // not applicable; terminate
        }

        public void Visit(JsIfNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsImportantComment node)
        {
            // not applicable; terminate
        }

        public void Visit(JsLabeledStatement node)
        {
            // not applicable; terminate
        }

        public void Visit(JsLexicalDeclaration node)
        {
            // not applicable; terminate
        }

        public void Visit(JsObjectLiteral node)
        {
            // not applicable; terminate
        }

        public void Visit(JsObjectLiteralField node)
        {
            // not applicable; terminate
        }

        public void Visit(JsObjectLiteralProperty node)
        {
            // not applicable; terminate
        }

        public void Visit(JsParameterDeclaration node)
        {
            // not applicable; terminate
        }

        public void Visit(JsRegExpLiteral node)
        {
            // not applicable; terminate
        }

        public void Visit(JsReturnNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsSwitch node)
        {
            // not applicable; terminate
        }

        public void Visit(JsSwitchCase node)
        {
            // not applicable; terminate
        }

        public void Visit(JsThisLiteral node)
        {
            // not applicable; terminate
        }

        public void Visit(JsThrowNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsTryNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsUnaryOperator node)
        {
            // not applicable; terminate
        }

        public void Visit(JsVar node)
        {
            // not applicable; terminate
        }

        public void Visit(JsVariableDeclaration node)
        {
            // not applicable; terminate
        }

        public void Visit(JsWhileNode node)
        {
            // not applicable; terminate
        }

        public void Visit(JsWithNode node)
        {
            // not applicable; terminate
        }

        #endregion
    }
}
