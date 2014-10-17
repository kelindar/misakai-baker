// conditional.cs
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

    internal sealed class JsConditional : JsExpression
    {
        private JsAstNode m_condition;
        private JsAstNode m_trueExpression;
        private JsAstNode m_falseExpression;

        public JsAstNode Condition
        {
            get { return m_condition; }
            set
            {
                m_condition.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_condition = value;
                m_condition.IfNotNull(n => n.Parent = this);
            }
        }

        public JsAstNode TrueExpression
        {
            get { return m_trueExpression; }
            set
            {
                m_trueExpression.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_trueExpression = value;
                m_trueExpression.IfNotNull(n => n.Parent = this);
            }
        }

        public JsAstNode FalseExpression
        {
            get { return m_falseExpression; }
            set
            {
                m_falseExpression.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_falseExpression = value;
                m_falseExpression.IfNotNull(n => n.Parent = this);
            }
        }

        public JsContext QuestionContext { get; set; }
        public JsContext ColonContext { get; set; }

        public JsConditional(JsContext context, JsParser parser)
            : base(context, parser)
        {
        }

        public override JsOperatorPrecedence Precedence
        {
            get
            {
                return JsOperatorPrecedence.Conditional;
            }
        }

        public void SwapBranches()
        {
            var temp = m_trueExpression;
            m_trueExpression = m_falseExpression;
            m_falseExpression = temp;
        }

        public override JsPrimitiveType FindPrimitiveType()
        {
            if (TrueExpression != null && FalseExpression != null)
            {
                // if the primitive type of both true and false expressions is the same, then
                // we know the primitive type. Otherwise we do not.
                JsPrimitiveType trueType = TrueExpression.FindPrimitiveType();
                if (trueType == FalseExpression.FindPrimitiveType())
                {
                    return trueType;
                }
            }

            // nope -- they don't match, so we don't know
            return JsPrimitiveType.Other;
        }

        public override bool IsEquivalentTo(JsAstNode otherNode)
        {
            var otherConditional = otherNode as JsConditional;
            return otherConditional != null
                && Condition.IsEquivalentTo(otherConditional.Condition)
                && TrueExpression.IsEquivalentTo(otherConditional.TrueExpression)
                && FalseExpression.IsEquivalentTo(otherConditional.FalseExpression);
        }

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Condition, TrueExpression, FalseExpression);
            }
        }

        public override void Accept(IJsVisitor visitor)
        {
            if (visitor != null)
            {
                visitor.Visit(this);
            }
        }

        public override bool ReplaceChild(JsAstNode oldNode, JsAstNode newNode)
        {
            if (Condition == oldNode)
            {
                Condition = newNode;
                return true;
            }
            if (TrueExpression == oldNode)
            {
                TrueExpression = newNode;
                return true;
            }
            if (FalseExpression == oldNode)
            {
                FalseExpression = newNode;
                return true;
            }
            return false;
        }

        public override JsAstNode LeftHandSide
        {
            get
            {
                // the condition is on the left
                return Condition.LeftHandSide;
            }
        }
    }
}