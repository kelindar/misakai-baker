// unaryop.cs
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

using System.Collections.Generic;
using System.Diagnostics;

namespace Baker.Text
{
    internal class JsUnaryOperator : JsExpression
    {
        private JsAstNode m_operand;

        public JsAstNode Operand
        {
            get { return m_operand; }
            set
            {
                m_operand.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_operand = value;
                m_operand.IfNotNull(n => n.Parent = this);
            }
        }

        public JsContext OperatorContext { get; set; }

        public JsToken OperatorToken { get; set; }
        public bool IsPostfix { get; set; }
        public bool OperatorInConditionalCompilationComment { get; set; }
        public bool ConditionalCommentContainsOn { get; set; }

        public JsUnaryOperator(JsContext context, JsParser parser)
            : base(context, parser)
        {
        }

        public override void Accept(IJsVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override JsPrimitiveType FindPrimitiveType()
        {
            switch (OperatorToken)
            {
                case JsToken.TypeOf:
                    // typeof ALWAYS returns type string
                    return JsPrimitiveType.String;

                case JsToken.LogicalNot:
                    // ! always return boolean
                    return JsPrimitiveType.Boolean;

                case JsToken.Void:
                case JsToken.Delete:
                    // void returns undefined.
                    // delete returns number, but just return other
                    return JsPrimitiveType.Other;

                default:
                    // all other unary operators return a number
                    return JsPrimitiveType.Number;
            }
        }

        public override JsOperatorPrecedence Precedence
        {
            get
            {
                // assume unary precedence
                return JsOperatorPrecedence.Unary;
            }
        }

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Operand);
            }
        }

        public override bool ReplaceChild(JsAstNode oldNode, JsAstNode newNode)
        {
            if (Operand == oldNode)
            {
                Operand = newNode;
                return true;
            }
            return false;
        }

        public override bool IsEquivalentTo(JsAstNode otherNode)
        {
            var otherUnary = otherNode as JsUnaryOperator;
            return otherUnary != null
                && OperatorToken == otherUnary.OperatorToken
                && Operand.IsEquivalentTo(otherUnary.Operand);
        }

        public override bool IsConstant
        {
            get
            {
                return Operand.IfNotNull(o => o.IsConstant);
            }
        }

        public override string ToString()
        {
            return JsOutputVisitor.OperatorString(OperatorToken)
                + (Operand == null ? "<null>" : Operand.ToString());
        }
    }
}
