// binaryop.cs
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
using System.Text;

namespace Baker.Text
{

    internal class JsBinaryOperator : JsExpression
    {
        private JsAstNode m_operand1;
        private JsAstNode m_operand2;

        public JsAstNode Operand1 
        {
            get { return m_operand1; }
            set
            {
                m_operand1.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_operand1 = value;
                m_operand1.IfNotNull(n => n.Parent = this);
            }
        }
        
        public JsAstNode Operand2 
        {
            get { return m_operand2; }
            set
            {
                m_operand2.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_operand2 = value;
                m_operand2.IfNotNull(n => n.Parent = this);
            }
        }

        public JsToken OperatorToken { get; set; }
        public JsContext OperatorContext { get; set; }

        public override JsContext TerminatingContext
        {
            get
            {
                // if we have one, return it. If not, see ifthe right-hand side has one
                return base.TerminatingContext ?? Operand2.IfNotNull(n => n.TerminatingContext);
            }
        }

        public JsBinaryOperator(JsContext context, JsParser parser)
            : base(context, parser)
        {
        }

        public override JsOperatorPrecedence Precedence
        {
            get 
            {
                switch (OperatorToken)
                {
                    case JsToken.Comma:
                        return JsOperatorPrecedence.Comma;

                    case JsToken.Assign:
                    case JsToken.BitwiseAndAssign:
                    case JsToken.BitwiseOrAssign:
                    case JsToken.BitwiseXorAssign:
                    case JsToken.DivideAssign:
                    case JsToken.LeftShiftAssign:
                    case JsToken.MinusAssign:
                    case JsToken.ModuloAssign:
                    case JsToken.MultiplyAssign:
                    case JsToken.RightShiftAssign:
                    case JsToken.UnsignedRightShiftAssign:
                    case JsToken.PlusAssign:
                        return JsOperatorPrecedence.Assignment;

                    case JsToken.LogicalOr:
                        return JsOperatorPrecedence.LogicalOr;

                    case JsToken.LogicalAnd:
                        return JsOperatorPrecedence.LogicalAnd;

                    case JsToken.BitwiseOr:
                        return JsOperatorPrecedence.BitwiseOr;

                    case JsToken.BitwiseXor:
                        return JsOperatorPrecedence.BitwiseXor;

                    case JsToken.BitwiseAnd:
                        return JsOperatorPrecedence.BitwiseAnd;

                    case JsToken.Equal:
                    case JsToken.NotEqual:
                    case JsToken.StrictEqual:
                    case JsToken.StrictNotEqual:
                        return JsOperatorPrecedence.Equality;

                    case JsToken.GreaterThan:
                    case JsToken.GreaterThanEqual:
                    case JsToken.In:
                    case JsToken.InstanceOf:
                    case JsToken.LessThan:
                    case JsToken.LessThanEqual:
                        return JsOperatorPrecedence.Relational;

                    case JsToken.LeftShift:
                    case JsToken.RightShift:
                    case JsToken.UnsignedRightShift:
                        return JsOperatorPrecedence.Shift;

                    case JsToken.Multiply:
                    case JsToken.Divide:
                    case JsToken.Modulo:
                        return JsOperatorPrecedence.Multiplicative;

                    case JsToken.Plus:
                    case JsToken.Minus:
                        return JsOperatorPrecedence.Additive;

                    default:
                        return JsOperatorPrecedence.None;
                }
            }
        }

        public override JsPrimitiveType FindPrimitiveType()
        {
            JsPrimitiveType leftType;
            JsPrimitiveType rightType;

            switch (OperatorToken)
            {
                case JsToken.Assign:
                case JsToken.Comma:
                    // returns whatever type the right operand is
                    return Operand2.FindPrimitiveType();

                case JsToken.BitwiseAnd:
                case JsToken.BitwiseAndAssign:
                case JsToken.BitwiseOr:
                case JsToken.BitwiseOrAssign:
                case JsToken.BitwiseXor:
                case JsToken.BitwiseXorAssign:
                case JsToken.Divide:
                case JsToken.DivideAssign:
                case JsToken.LeftShift:
                case JsToken.LeftShiftAssign:
                case JsToken.Minus:
                case JsToken.MinusAssign:
                case JsToken.Modulo:
                case JsToken.ModuloAssign:
                case JsToken.Multiply:
                case JsToken.MultiplyAssign:
                case JsToken.RightShift:
                case JsToken.RightShiftAssign:
                case JsToken.UnsignedRightShift:
                case JsToken.UnsignedRightShiftAssign:
                    // always returns a number
                    return JsPrimitiveType.Number;

                case JsToken.Equal:
                case JsToken.GreaterThan:
                case JsToken.GreaterThanEqual:
                case JsToken.In:
                case JsToken.InstanceOf:
                case JsToken.LessThan:
                case JsToken.LessThanEqual:
                case JsToken.NotEqual:
                case JsToken.StrictEqual:
                case JsToken.StrictNotEqual:
                    // always returns a boolean
                    return JsPrimitiveType.Boolean;

                case JsToken.PlusAssign:
                case JsToken.Plus:
                    // if either operand is known to be a string, then the result type is a string.
                    // otherwise the result is numeric if both types are known.
                    leftType = Operand1.FindPrimitiveType();
                    rightType = Operand2.FindPrimitiveType();

                    return (leftType == JsPrimitiveType.String || rightType == JsPrimitiveType.String)
                        ? JsPrimitiveType.String
                        : (leftType != JsPrimitiveType.Other && rightType != JsPrimitiveType.Other
                            ? JsPrimitiveType.Number
                            : JsPrimitiveType.Other);

                case JsToken.LogicalAnd:
                case JsToken.LogicalOr:
                    // these two are special. They return either the left or the right operand
                    // (depending on their values), so unless they are both known types AND the same,
                    // then we can't know for sure.
                    leftType = Operand1.FindPrimitiveType();
                    if (leftType != JsPrimitiveType.Other)
                    {
                        if (leftType == Operand2.FindPrimitiveType())
                        {
                            // they are both the same and neither is unknown
                            return leftType;
                        }
                    }

                    // if we get here, then we don't know the type
                    return JsPrimitiveType.Other;

                default:
                    // shouldn't get here....
                    return JsPrimitiveType.Other;
            }
        }

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Operand1, Operand2);
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
            if (Operand1 == oldNode)
            {
                Operand1 = newNode;
                return true;
            }
            if (Operand2 == oldNode)
            {
                Operand2 = newNode;
                return true;
            }
            return false;
        }

        public override JsAstNode LeftHandSide
        {
            get
            {
                if (OperatorToken == JsToken.Comma)
                {
                    // for comma-operators, the leftmost item is the leftmost item of the
                    // rightmost operand. And the operand2 might be a list.
                    var list = Operand2 as JsAstNodeList;
                    if (list != null && list.Count > 0)
                    {
                        // the right-hand side is a list, so we want the LAST item
                        return list[list.Count - 1].LeftHandSide;
                    }

                    // not a list, just ask the right-hand operand what its leftmost node is
                    return Operand2.LeftHandSide;
                }

                // not a comma, so operand1 is on the left
                return Operand1.LeftHandSide;
            }
        }

        public void SwapOperands()
        {
            // swap the operands -- we don't need to go through ReplaceChild or the
            // property setters because we don't need to change the Parent pointers 
            // or anything like that.
            JsAstNode temp = m_operand1;
            m_operand1 = m_operand2;
            m_operand2 = temp;
        }

        public override bool IsEquivalentTo(JsAstNode otherNode)
        {
            // a binary operator is equivalent to another binary operator if the operator is the same and
            // both operands are also equivalent
            var otherBinary = otherNode as JsBinaryOperator;
            return otherBinary != null
                && OperatorToken == otherBinary.OperatorToken
                && Operand1.IsEquivalentTo(otherBinary.Operand1)
                && Operand2.IsEquivalentTo(otherBinary.Operand2);
        }

        public bool IsAssign
        {
            get
            {
                switch(OperatorToken)
                {
                    case JsToken.Assign:
                    case JsToken.PlusAssign:
                    case JsToken.MinusAssign:
                    case JsToken.MultiplyAssign:
                    case JsToken.DivideAssign:
                    case JsToken.ModuloAssign:
                    case JsToken.BitwiseAndAssign:
                    case JsToken.BitwiseOrAssign:
                    case JsToken.BitwiseXorAssign:
                    case JsToken.LeftShiftAssign:
                    case JsToken.RightShiftAssign:
                    case JsToken.UnsignedRightShiftAssign:
                        return true;

                    default:
                        return false;
                }
            }
        }

        internal override string GetFunctionGuess(JsAstNode target)
        {
            return Operand2 == target
                ? IsAssign ? Operand1.GetFunctionGuess(this) : Parent.GetFunctionGuess(this)
                : string.Empty;
        }

        /// <summary>
        /// Returns true if the expression contains an in-operator
        /// </summary>
        public override bool ContainsInOperator
        {
            get
            {
                // if we are an in-operator, then yeah: we contain one.
                // otherwise recurse the operands.
                return OperatorToken == JsToken.In
                    ? true
                    : Operand1.ContainsInOperator || Operand2.ContainsInOperator;
            }
        }

        public override bool IsConstant
        {
            get
            {
                return Operand1.IfNotNull(o => o.IsConstant) && Operand2.IfNotNull(o => o.IsConstant);
            }
        }

        public override string ToString()
        {
            return (Operand1 == null ? "<null>" : Operand1.ToString())
                + ' ' + JsOutputVisitor.OperatorString(OperatorToken) + ' '
                + (Operand2 == null ? "<null>" : Operand2.ToString());
        }
    }
}
