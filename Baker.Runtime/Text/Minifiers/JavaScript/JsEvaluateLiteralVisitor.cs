// EvaluateLiteralVisitor.cs
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
using System.Text;

namespace Baker.Text
{
    internal class JsEvaluateLiteralVisitor : JsTreeVisitor
    {
        private JsParser m_parser;

        public JsEvaluateLiteralVisitor(JsParser parser) 
        {
            m_parser = parser;
        }

        #region BinaryOperator helper methods

        /// <summary>
        /// If the new literal is a string literal, then we need to check to see if our
        /// parent is a CallNode. If it is, and if the string literal can be an identifier,
        /// we'll replace it with a Member-Dot operation.
        /// </summary>
        /// <param name="newLiteral">newLiteral we intend to replace this binaryop node with</param>
        /// <returns>true if we replaced the parent callnode with a member-dot operation</returns>
        /// <param name="node"></param>
        private bool ReplaceMemberBracketWithDot(JsBinaryOperator node, JsConstantWrapper newLiteral)
        {
            if (newLiteral.IsStringLiteral)
            {
                // see if this newly-combined string is the sole argument to a 
                // call-brackets node. If it is and the combined string is a valid
                // identifier (and not a keyword), then we can replace the call
                // with a member operator.
                // remember that the parent of the argument won't be the call node -- it
                // will be the ast node list representing the arguments, whose parent will
                // be the node list. 
                JsCallNode parentCall = (node.Parent is JsAstNodeList ? node.Parent.Parent as JsCallNode : null);
                if (parentCall != null && parentCall.InBrackets)
                {
                    // get the newly-combined string
                    string combinedString = newLiteral.ToString();

                    // see if this new string is the target of a replacement operation
                    string newName;
                    if (m_parser.Settings.HasRenamePairs && m_parser.Settings.ManualRenamesProperties
                        && m_parser.Settings.IsModificationAllowed(JsTreeModifications.PropertyRenaming)
                        && !string.IsNullOrEmpty(newName = m_parser.Settings.GetNewName(combinedString)))
                    {
                        // yes, it is. Now see if the new name is safe to be converted to a dot-operation.
                        if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.BracketMemberToDotMember)
                            && JsScanner.IsSafeIdentifier(newName)
                            && !JsScanner.IsKeyword(newName, parentCall.EnclosingScope.UseStrict))
                        {
                            // we want to replace the call with operator with a new member dot operation, and
                            // since we won't be analyzing it (we're past the analyze phase, we're going to need
                            // to use the new string value
                            JsMember replacementMember = new JsMember(parentCall.Context, m_parser)
                                {
                                    Root = parentCall.Function,
                                    Name = newName,
                                    NameContext = parentCall.Arguments[0].Context
                                };
                            parentCall.Parent.ReplaceChild(parentCall, replacementMember);
                            return true;
                        }
                        else
                        {
                            // nope, can't be changed to a dot-operator for whatever reason.
                            // just replace the value on this new literal. The old operation will
                            // get replaced with this new literal
                            newLiteral.Value = newName;

                            // and make sure it's type is string
                            newLiteral.PrimitiveType = JsPrimitiveType.String;
                        }
                    }
                    else if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.BracketMemberToDotMember))
                    {
                        // our parent is a call-bracket -- now we just need to see if the newly-combined
                        // string can be an identifier
                        if (JsScanner.IsSafeIdentifier(combinedString) && !JsScanner.IsKeyword(combinedString, parentCall.EnclosingScope.UseStrict))
                        {
                            // yes -- replace the parent call with a new member node using the newly-combined string
                            JsMember replacementMember = new JsMember(parentCall.Context, m_parser)
                                {
                                    Root = parentCall.Function,
                                    Name = combinedString,
                                    NameContext = parentCall.Arguments[0].Context
                                };
                            parentCall.Parent.ReplaceChild(parentCall, replacementMember);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// replace the node with a literal. If the node was wrapped in a grouping operator
        /// before (parentheses around it), then we can get rid of the parentheses too, since
        /// we are replacing the node with a single literal entity.
        /// </summary>
        /// <param name="node">node to replace</param>
        /// <param name="newLiteral">literal to replace the node with</param>
        private static void ReplaceNodeWithLiteral(JsAstNode node, JsConstantWrapper newLiteral)
        {
            var grouping = node.Parent as JsGroupingOperator;
            if (grouping != null)
            {
                // because we are replacing the operator with a literal, the parentheses
                // the grouped this operator are now superfluous. Replace them, too
                grouping.Parent.ReplaceChild(grouping, newLiteral);
            }
            else
            {
                // just replace the node with the literal
                node.Parent.ReplaceChild(node, newLiteral);
            }
        }

        private static void ReplaceNodeCheckParens(JsAstNode oldNode, JsAstNode newNode)
        {
            var grouping = oldNode.Parent as JsGroupingOperator;
            if (grouping != null)
            {
                if (newNode != null)
                {
                    var targetPrecedence = grouping.Parent.Precedence;
                    var conditional = grouping.Parent as JsConditional;
                    if (conditional != null)
                    {
                        // the conditional is weird in that the different parts need to be
                        // compared against different precedences, not the precedence of the
                        // conditional itself. The condition should be compared to logical-or,
                        // and the true/false expressions against assignment.
                        targetPrecedence = conditional.Condition == grouping
                            ? JsOperatorPrecedence.LogicalOr
                            : JsOperatorPrecedence.Assignment;
                    }

                    if (newNode.Precedence >= targetPrecedence)
                    {
                        // don't need the parens anymore, so replace the grouping operator
                        // with the new node, thereby eliminating the parens
                        grouping.Parent.ReplaceChild(grouping, newNode);
                    }
                    else
                    {
                        // still need the parens; just replace the node with the literal
                        oldNode.Parent.ReplaceChild(oldNode, newNode);
                    }
                }
                else
                {
                    // eliminate the parens
                    grouping.Parent.ReplaceChild(grouping, null);
                }
            }
            else
            {
                // just replace the node with the literal
                oldNode.Parent.ReplaceChild(oldNode, newNode);
            }
        }

        /// <summary>
        /// Both the operands of this operator are constants. See if we can evaluate them
        /// </summary>
        /// <param name="left">left-side operand</param>
        /// <param name="right">right-side operand</param>
        /// <param name="node"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void EvalThisOperator(JsBinaryOperator node, JsConstantWrapper left, JsConstantWrapper right)
        {
            // we can evaluate these operators if we know both operands are literal
            // number, boolean, string or null
            JsConstantWrapper newLiteral = null;
            switch (node.OperatorToken)
            {
                case JsToken.Multiply:
                    newLiteral = Multiply(left, right);
                    break;

                case JsToken.Divide:
                    newLiteral = Divide(left, right);
                    if (newLiteral != null && newLiteral.ToCode().Length > node.ToCode().Length)
                    {
                        // the result is bigger than the expression.
                        // eg: 1/3 is smaller than .333333333333333
                        // never mind.
                        newLiteral = null;
                    }
                    break;

                case JsToken.Modulo:
                    newLiteral = Modulo(left, right);
                    if (newLiteral != null && newLiteral.ToCode().Length > node.ToCode().Length)
                    {
                        // the result is bigger than the expression.
                        // eg: 46.5%6.3 is smaller than 2.4000000000000012
                        // never mind.
                        newLiteral = null;
                    }
                    break;

                case JsToken.Plus:
                    newLiteral = Plus(left, right);
                    break;

                case JsToken.Minus:
                    newLiteral = Minus(left, right);
                    break;

                case JsToken.LeftShift:
                    newLiteral = LeftShift(left, right);
                    break;

                case JsToken.RightShift:
                    newLiteral = RightShift(left, right);
                    break;

                case JsToken.UnsignedRightShift:
                    newLiteral = UnsignedRightShift(left, right);
                    break;

                case JsToken.LessThan:
                    newLiteral = LessThan(left, right);
                    break;

                case JsToken.LessThanEqual:
                    newLiteral = LessThanOrEqual(left, right);
                    break;

                case JsToken.GreaterThan:
                    newLiteral = GreaterThan(left, right);
                    break;

                case JsToken.GreaterThanEqual:
                    newLiteral = GreaterThanOrEqual(left, right);
                    break;

                case JsToken.Equal:
                    newLiteral = Equal(left, right);
                    break;

                case JsToken.NotEqual:
                    newLiteral = NotEqual(left, right);
                    break;

                case JsToken.StrictEqual:
                    newLiteral = StrictEqual(left, right);
                    break;

                case JsToken.StrictNotEqual:
                    newLiteral = StrictNotEqual(left, right);
                    break;

                case JsToken.BitwiseAnd:
                    newLiteral = BitwiseAnd(left, right);
                    break;

                case JsToken.BitwiseOr:
                    newLiteral = BitwiseOr(left, right);
                    break;

                case JsToken.BitwiseXor:
                    newLiteral = BitwiseXor(left, right);
                    break;

                case JsToken.LogicalAnd:
                    newLiteral = LogicalAnd(left, right);
                    break;

                case JsToken.LogicalOr:
                    newLiteral = LogicalOr(left, right);
                    break;

                default:
                    // an operator we don't want to evaluate
                    break;
            }

            // if we can combine them...
            if (newLiteral != null)
            {
                // first we want to check if the new combination is a string literal, and if so, whether 
                // it's now the sole parameter of a member-bracket call operator. If so, instead of replacing our
                // binary operation with the new constant, we'll replace the entire call with a member-dot
                // expression
                if (!ReplaceMemberBracketWithDot(node, newLiteral))
                {
                    ReplaceNodeWithLiteral(node, newLiteral);
                }
            }
        }

        /// <summary>
        /// We have determined that our left-hand operand is another binary operator, and its
        /// right-hand operand is a constant that can be combined with our right-hand operand.
        /// Now we want to set the right-hand operand of that other operator to the newly-
        /// combined constant value, and then rotate it up -- replace our binary operator
        /// with this newly-modified binary operator, and then attempt to re-evaluate it.
        /// </summary>
        /// <param name="binaryOp">the binary operator that is our left-hand operand</param>
        /// <param name="newLiteral">the newly-combined literal</param>
        /// <param name="node"></param>
        private void RotateFromLeft(JsBinaryOperator node, JsBinaryOperator binaryOp, JsConstantWrapper newLiteral)
        {
            // replace our node with the binary operator
            binaryOp.Operand2 = newLiteral;
            node.Parent.ReplaceChild(node, binaryOp);

            // and just for good measure.. revisit the node that's taking our place, since
            // we just changed a constant value. Assuming the other operand is a constant, too.
            JsConstantWrapper otherConstant = binaryOp.Operand1 as JsConstantWrapper;
            if (otherConstant != null)
            {
                EvalThisOperator(binaryOp, otherConstant, newLiteral);
            }
        }

        /// <summary>
        /// We have determined that our right-hand operand is another binary operator, and its
        /// left-hand operand is a constant that can be combined with our left-hand operand.
        /// Now we want to set the left-hand operand of that other operator to the newly-
        /// combined constant value, and then rotate it up -- replace our binary operator
        /// with this newly-modified binary operator, and then attempt to re-evaluate it.
        /// </summary>
        /// <param name="binaryOp">the binary operator that is our right-hand operand</param>
        /// <param name="newLiteral">the newly-combined literal</param>
        /// <param name="node"></param>
        private void RotateFromRight(JsBinaryOperator node, JsBinaryOperator binaryOp, JsConstantWrapper newLiteral)
        {
            // replace our node with the binary operator
            binaryOp.Operand1 = newLiteral;
            node.Parent.ReplaceChild(node, binaryOp);

            // and just for good measure.. revisit the node that's taking our place, since
            // we just changed a constant value. Assuming the other operand is a constant, too.
            JsConstantWrapper otherConstant = binaryOp.Operand2 as JsConstantWrapper;
            if (otherConstant != null)
            {
                EvalThisOperator(binaryOp, newLiteral, otherConstant);
            }
        }

        /// <summary>
        /// Return true is not an overflow or underflow, for multiplication operations
        /// </summary>
        /// <param name="left">left operand</param>
        /// <param name="right">right operand</param>
        /// <param name="result">result</param>
        /// <returns>true if result not overflow or underflow; false if it is</returns>
        private static bool NoMultiplicativeOverOrUnderFlow(JsConstantWrapper left, JsConstantWrapper right, JsConstantWrapper result)
        {
            // check for overflow
            bool okayToProceed = !result.IsInfinity;

            // if we still might be good, check for possible underflow
            if (okayToProceed)
            {
                // if the result is zero, we might have an underflow. But if one of the operands
                // was zero, then it's okay.
                // Inverse: if neither operand is zero, then a zero result is not okay
                okayToProceed = !result.IsZero || (left.IsZero || right.IsZero);
            }
            return okayToProceed;
        }

        /// <summary>
        /// Return true if the result isn't an overflow condition
        /// </summary>
        /// <param name="result">result constant</param>
        /// <returns>true is not an overflow; false if it is</returns>
        private static bool NoOverflow(JsConstantWrapper result)
        {
            return !result.IsInfinity;
        }

        /// <summary>
        /// Evaluate: (OTHER [op] CONST) [op] CONST
        /// </summary>
        /// <param name="thisConstant">second constant</param>
        /// <param name="otherConstant">first constant</param>
        /// <param name="leftOperator">first operator</param>
        /// <param name="node"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void EvalToTheLeft(JsBinaryOperator node, JsConstantWrapper thisConstant, JsConstantWrapper otherConstant, JsBinaryOperator leftOperator)
        {
            if (leftOperator.OperatorToken == JsToken.Plus && node.OperatorToken == JsToken.Plus)
            {
                // plus-plus
                // the other operation goes first, so if the other constant is a string, then we know that
                // operation will do a string concatenation, which will force our operation to be a string
                // concatenation. If the other constant is not a string, then we won't know until runtime and
                // we can't combine them.
                if (otherConstant.IsStringLiteral)
                {
                    // the other constant is a string -- so we can do the string concat and combine them
                    JsConstantWrapper newLiteral = StringConcat(otherConstant, thisConstant);
                    if (newLiteral != null)
                    {
                        RotateFromLeft(node, leftOperator, newLiteral);
                    }
                }
            }
            else if (leftOperator.OperatorToken == JsToken.Minus)
            {
                if (node.OperatorToken == JsToken.Plus)
                {
                    // minus-plus
                    // the minus operator goes first and will always convert to number.
                    // if our constant is not a string, then it will be a numeric addition and we can combine them.
                    // if our constant is a string, then we'll end up doing a string concat, so we can't combine
                    if (!thisConstant.IsStringLiteral)
                    {
                        // two numeric operators. a-n1+n2 is the same as a-(n1-n2)
                        JsConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                        if (newLiteral != null && NoOverflow(newLiteral))
                        {
                            // a-(-n) is numerically equivalent as a+n -- and takes fewer characters to represent.
                            // BUT we can't do that because that might change a numeric operation (the original minus)
                            // to a string concatenation if the unknown operand turns out to be a string!

                            RotateFromLeft(node, leftOperator, newLiteral);
                        }
                        else
                        {
                            // if the left-left is a constant, then we can try combining with it
                            JsConstantWrapper leftLeft = leftOperator.Operand1 as JsConstantWrapper;
                            if (leftLeft != null)
                            {
                                EvalFarToTheLeft(node, thisConstant, leftLeft, leftOperator);
                            }
                        }
                    }
                }
                else if (node.OperatorToken == JsToken.Minus)
                {
                    // minus-minus. Both operations are numeric.
                    // (a-n1)-n2 => a-(n1+n2), so we can add the two constants and subtract from 
                    // the left-hand non-constant. 
                    JsConstantWrapper newLiteral = NumericAddition(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        // make it the new right-hand literal for the left-hand operator
                        // and make the left-hand operator replace our operator
                        RotateFromLeft(node, leftOperator, newLiteral);
                    }
                    else
                    {
                        // if the left-left is a constant, then we can try combining with it
                        JsConstantWrapper leftLeft = leftOperator.Operand1 as JsConstantWrapper;
                        if (leftLeft != null)
                        {
                            EvalFarToTheLeft(node, thisConstant, leftLeft, leftOperator);
                        }
                    }
                }
            }
            else if (leftOperator.OperatorToken == node.OperatorToken
                && (node.OperatorToken == JsToken.Multiply || node.OperatorToken == JsToken.Divide))
            {
                // either multiply-multiply or divide-divide
                // either way, we use the other operand and the product of the two constants.
                // if the product blows up to an infinte value, then don't combine them because that
                // could change the way the program goes at runtime, depending on the unknown value.
                JsConstantWrapper newLiteral = Multiply(otherConstant, thisConstant);
                if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral))
                {
                    RotateFromLeft(node, leftOperator, newLiteral);
                }
            }
            else if ((leftOperator.OperatorToken == JsToken.Multiply && node.OperatorToken == JsToken.Divide)
                || (leftOperator.OperatorToken == JsToken.Divide && node.OperatorToken == JsToken.Multiply))
            {
                if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
                {
                    // get the two division operators
                    JsConstantWrapper otherOverThis = Divide(otherConstant, thisConstant);
                    JsConstantWrapper thisOverOther = Divide(thisConstant, otherConstant);

                    // get the lengths
                    int otherOverThisLength = otherOverThis != null ? otherOverThis.ToCode().Length : int.MaxValue;
                    int thisOverOtherLength = thisOverOther != null ? thisOverOther.ToCode().Length : int.MaxValue;

                    // we'll want to use whichever one is shorter, and whichever one does NOT involve an overflow 
                    // or possible underflow
                    if (otherOverThis != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, otherOverThis)
                        && (thisOverOther == null || otherOverThisLength < thisOverOtherLength))
                    {
                        // but only if it's smaller than the original expression
                        if (otherOverThisLength <= otherConstant.ToCode().Length + thisConstant.ToCode().Length + 1)
                        {
                            // same operator
                            RotateFromLeft(node, leftOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        // but only if it's smaller than the original expression
                        if (thisOverOtherLength <= otherConstant.ToCode().Length + thisConstant.ToCode().Length + 1)
                        {
                            // opposite operator
                            leftOperator.OperatorToken = leftOperator.OperatorToken == JsToken.Multiply ? JsToken.Divide : JsToken.Multiply;
                            RotateFromLeft(node, leftOperator, thisOverOther);
                        }
                    }
                }
            }
            else if (node.OperatorToken == leftOperator.OperatorToken
                && (node.OperatorToken == JsToken.BitwiseAnd || node.OperatorToken == JsToken.BitwiseOr || node.OperatorToken == JsToken.BitwiseXor))
            {
                // identical bitwise operators can be combined
                JsConstantWrapper newLiteral = null;
                switch (node.OperatorToken)
                {
                    case JsToken.BitwiseAnd:
                        newLiteral = BitwiseAnd(otherConstant, thisConstant);
                        break;

                    case JsToken.BitwiseOr:
                        newLiteral = BitwiseOr(otherConstant, thisConstant);
                        break;

                    case JsToken.BitwiseXor:
                        newLiteral = BitwiseXor(otherConstant, thisConstant);
                        break;
                }
                if (newLiteral != null)
                {
                    RotateFromLeft(node, leftOperator, newLiteral);
                }
            }
        }

        /// <summary>
        /// Evaluate: (CONST [op] OTHER) [op] CONST
        /// </summary>
        /// <param name="thisConstant">second constant</param>
        /// <param name="otherConstant">first constant</param>
        /// <param name="leftOperator">first operator</param>
        /// <param name="node"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void EvalFarToTheLeft(JsBinaryOperator node, JsConstantWrapper thisConstant, JsConstantWrapper otherConstant, JsBinaryOperator leftOperator)
        {
            if (leftOperator.OperatorToken == JsToken.Minus)
            {
                if (node.OperatorToken == JsToken.Plus)
                {
                    // minus-plus
                    // the minus will be a numeric operator, but if this constant is a string, it will be a
                    // string concatenation and we can't combine it.
                    if (thisConstant.PrimitiveType != JsPrimitiveType.String && thisConstant.PrimitiveType != JsPrimitiveType.Other)
                    {
                        JsConstantWrapper newLiteral = NumericAddition(otherConstant, thisConstant);
                        if (newLiteral != null && NoOverflow(newLiteral))
                        {
                            RotateFromRight(node, leftOperator, newLiteral);
                        }
                    }
                }
                else if (node.OperatorToken == JsToken.Minus)
                {
                    // minus-minus
                    JsConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
            }
            else if (node.OperatorToken == JsToken.Multiply)
            {
                if (leftOperator.OperatorToken == JsToken.Multiply || leftOperator.OperatorToken == JsToken.Divide)
                {
                    JsConstantWrapper newLiteral = Multiply(otherConstant, thisConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral))
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
            }
            else if (node.OperatorToken == JsToken.Divide)
            {
                if (leftOperator.OperatorToken == JsToken.Divide)
                {
                    // divide-divide
                    JsConstantWrapper newLiteral = Divide(otherConstant, thisConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, newLiteral)
                        && newLiteral.ToCode().Length <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        RotateFromRight(node, leftOperator, newLiteral);
                    }
                }
                else if (leftOperator.OperatorToken == JsToken.Multiply)
                {
                    // mult-divide
                    JsConstantWrapper otherOverThis = Divide(otherConstant, thisConstant);
                    JsConstantWrapper thisOverOther = Divide(thisConstant, otherConstant);

                    int otherOverThisLength = otherOverThis != null ? otherOverThis.ToCode().Length : int.MaxValue;
                    int thisOverOtherLength = thisOverOther != null ? thisOverOther.ToCode().Length : int.MaxValue;

                    if (otherOverThis != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, otherOverThis)
                        && (thisOverOther == null || otherOverThisLength < thisOverOtherLength))
                    {
                        if (otherOverThisLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            RotateFromRight(node, leftOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        if (thisOverOtherLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // swap the operands
                            leftOperator.SwapOperands();

                            // operator is the opposite
                            leftOperator.OperatorToken = JsToken.Divide;
                            RotateFromLeft(node, leftOperator, thisOverOther);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Evaluate: CONST [op] (CONST [op] OTHER)
        /// </summary>
        /// <param name="thisConstant">first constant</param>
        /// <param name="otherConstant">second constant</param>
        /// <param name="rightOperator">second operator</param>
        /// <param name="node"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void EvalToTheRight(JsBinaryOperator node, JsConstantWrapper thisConstant, JsConstantWrapper otherConstant, JsBinaryOperator rightOperator)
        {
            if (node.OperatorToken == JsToken.Plus)
            {
                if (rightOperator.OperatorToken == JsToken.Plus && otherConstant.IsStringLiteral)
                {
                    // plus-plus, and the other constant is a string. So the right operator will be a string-concat
                    // that generates a string. And since this is a plus-operator, then this operator will be a string-
                    // concat as well. So we can just combine the strings now and replace our node with the right-hand 
                    // operation
                    JsConstantWrapper newLiteral = StringConcat(thisConstant, otherConstant);
                    if (newLiteral != null)
                    {
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JsToken.Minus && !thisConstant.IsStringLiteral)
                {
                    // plus-minus. Now, the minus operation happens first, and it will perform a numeric
                    // operation. The plus is NOT string, so that means it will also be a numeric operation
                    // and we can combine the operators numericly. 
                    JsConstantWrapper newLiteral = NumericAddition(thisConstant, otherConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                    else
                    {
                        JsConstantWrapper rightRight = rightOperator.Operand2 as JsConstantWrapper;
                        if (rightRight != null)
                        {
                            EvalFarToTheRight(node, thisConstant, rightRight, rightOperator);
                        }
                    }
                }
            }
            else if (node.OperatorToken == JsToken.Minus && rightOperator.OperatorToken == JsToken.Minus)
            {
                // minus-minus
                // both operations are numeric, so we can combine the constant operands. However, we 
                // can't combine them into a plus, so make sure we do the minus in the opposite direction
                JsConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                if (newLiteral != null && NoOverflow(newLiteral))
                {
                    rightOperator.SwapOperands();
                    RotateFromLeft(node, rightOperator, newLiteral);
                }
                else
                {
                    JsConstantWrapper rightRight = rightOperator.Operand2 as JsConstantWrapper;
                    if (rightRight != null)
                    {
                        EvalFarToTheRight(node, thisConstant, rightRight, rightOperator);
                    }
                }
            }
            else if (node.OperatorToken == JsToken.Multiply
                && (rightOperator.OperatorToken == JsToken.Multiply || rightOperator.OperatorToken == JsToken.Divide))
            {
                // multiply-divide or multiply-multiply
                // multiply the operands and use the right-hand operator
                JsConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                {
                    RotateFromRight(node, rightOperator, newLiteral);
                }
            }
            else if (node.OperatorToken == JsToken.Divide)
            {
                if (rightOperator.OperatorToken == JsToken.Multiply)
                {
                    // divide-multiply
                    JsConstantWrapper newLiteral = Divide(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral)
                        && newLiteral.ToCode().Length < thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        // flip the operator: multiply becomes divide; devide becomes multiply
                        rightOperator.OperatorToken = JsToken.Divide;

                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JsToken.Divide)
                {
                    // divide-divide
                    // get constants for left/right and for right/left
                    JsConstantWrapper leftOverRight = Divide(thisConstant, otherConstant);
                    JsConstantWrapper rightOverLeft = Divide(otherConstant, thisConstant);

                    // get the lengths of the resulting code
                    int leftOverRightLength = leftOverRight != null ? leftOverRight.ToCode().Length : int.MaxValue;
                    int rightOverLeftLength = rightOverLeft != null ? rightOverLeft.ToCode().Length : int.MaxValue;

                    // try whichever is smaller
                    if (leftOverRight != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, leftOverRight)
                        && (rightOverLeft == null || leftOverRightLength < rightOverLeftLength))
                    {
                        // use left-over-right. 
                        // but only if the resulting value is smaller than the original expression
                        if (leftOverRightLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // We don't need to swap the operands, but we do need to switch the operator
                            rightOperator.OperatorToken = JsToken.Multiply;
                            RotateFromRight(node, rightOperator, leftOverRight);
                        }
                    }
                    else if (rightOverLeft != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, rightOverLeft))
                    {
                        // but only if the resulting value is smaller than the original expression
                        if (rightOverLeftLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // use right-over-left. Keep the operator, but swap the operands
                            rightOperator.SwapOperands();
                            RotateFromLeft(node, rightOperator, rightOverLeft);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Eval the two constants: CONST [op] (OTHER [op] CONST)
        /// </summary>
        /// <param name="thisConstant">first constant</param>
        /// <param name="otherConstant">second constant</param>
        /// <param name="rightOperator">second operator</param>
        /// <param name="node"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void EvalFarToTheRight(JsBinaryOperator node, JsConstantWrapper thisConstant, JsConstantWrapper otherConstant, JsBinaryOperator rightOperator)
        {
            if (rightOperator.OperatorToken == JsToken.Minus)
            {
                if (node.OperatorToken == JsToken.Plus)
                {
                    // plus-minus
                    // our constant cannot be a string, though
                    if (!thisConstant.IsStringLiteral)
                    {
                        JsConstantWrapper newLiteral = Minus(otherConstant, thisConstant);
                        if (newLiteral != null && NoOverflow(newLiteral))
                        {
                            RotateFromLeft(node, rightOperator, newLiteral);
                        }
                    }
                }
                else if (node.OperatorToken == JsToken.Minus)
                {
                    // minus-minus
                    JsConstantWrapper newLiteral = NumericAddition(thisConstant, otherConstant);
                    if (newLiteral != null && NoOverflow(newLiteral))
                    {
                        // but we need to swap the left and right operands first
                        rightOperator.SwapOperands();

                        // then rotate the node up after replacing old with new
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
            }
            else if (node.OperatorToken == JsToken.Multiply)
            {
                if (rightOperator.OperatorToken == JsToken.Multiply)
                {
                    // mult-mult
                    JsConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                    {
                        RotateFromLeft(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JsToken.Divide)
                {
                    // mult-divide
                    JsConstantWrapper otherOverThis = Divide(otherConstant, thisConstant);
                    JsConstantWrapper thisOverOther = Divide(thisConstant, otherConstant);

                    int otherOverThisLength = otherOverThis != null ? otherOverThis.ToCode().Length : int.MaxValue;
                    int thisOverOtherLength = thisOverOther != null ? thisOverOther.ToCode().Length : int.MaxValue;

                    if (otherOverThis != null && NoMultiplicativeOverOrUnderFlow(otherConstant, thisConstant, otherOverThis)
                        && (thisOverOther == null || otherOverThisLength < thisOverOtherLength))
                    {
                        if (otherOverThisLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // swap the operands, but keep the operator
                            RotateFromLeft(node, rightOperator, otherOverThis);
                        }
                    }
                    else if (thisOverOther != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, thisOverOther))
                    {
                        if (thisOverOtherLength <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                        {
                            // swap the operands and opposite operator
                            rightOperator.SwapOperands();
                            rightOperator.OperatorToken = JsToken.Multiply;
                            RotateFromRight(node, rightOperator, thisOverOther);
                        }
                    }
                }
            }
            else if (node.OperatorToken == JsToken.Divide)
            {
                if (rightOperator.OperatorToken == JsToken.Multiply)
                {
                    // divide-mult
                    JsConstantWrapper newLiteral = Divide(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral)
                        && newLiteral.ToCode().Length <= thisConstant.ToCode().Length + otherConstant.ToCode().Length + 1)
                    {
                        // swap the operands
                        rightOperator.SwapOperands();

                        // change the operator
                        rightOperator.OperatorToken = JsToken.Divide;
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
                else if (rightOperator.OperatorToken == JsToken.Divide)
                {
                    // divide-divide
                    JsConstantWrapper newLiteral = Multiply(thisConstant, otherConstant);
                    if (newLiteral != null && NoMultiplicativeOverOrUnderFlow(thisConstant, otherConstant, newLiteral))
                    {
                        // but we need to swap the left and right operands first
                        rightOperator.SwapOperands();

                        // then rotate the node up after replacing old with new
                        RotateFromRight(node, rightOperator, newLiteral);
                    }
                }
            }
        }

        #endregion

        #region Constant operation methods

        private JsConstantWrapper Multiply(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue * rightValue;

                    if (JsConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new JsConstantWrapper(leftValue, JsPrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new JsConstantWrapper(rightValue, JsPrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper Divide(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue / rightValue;

                    if (JsConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new JsConstantWrapper(leftValue, JsPrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new JsConstantWrapper(rightValue, JsPrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper Modulo(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue % rightValue;

                    if (JsConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new JsConstantWrapper(leftValue, JsPrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new JsConstantWrapper(rightValue, JsPrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper Plus(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (left.IsStringLiteral || right.IsStringLiteral)
            {
                // one or both are strings -- this is a strng concat operation
                newLiteral = StringConcat(left, right);
            }
            else
            {
                // neither are strings -- this is a numeric addition operation
                newLiteral = NumericAddition(left, right);
            }
            return newLiteral;
        }

        private JsConstantWrapper NumericAddition(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue + rightValue;

                    if (JsConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new JsConstantWrapper(leftValue, JsPrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new JsConstantWrapper(rightValue, JsPrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper StringConcat(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            // if we don't want to combine adjacent string literals, then we know we don't want to do
            // anything here.
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.CombineAdjacentStringLiterals))
            {
                // if either one of the operands is not a string literal, then check to see if we allow
                // evaluation of numeric expression; if not, then no-go. IF they are both string literals,
                // then it doesn't matter what the numeric flag says.
                if ((left.IsStringLiteral && right.IsStringLiteral)
                    || m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
                {
                    // if either value is a floating-point number (a number, not NaN, not Infinite, not an Integer),
                    // then we won't do the string concatenation because different browsers may have subtle differences
                    // in their double-to-string conversion algorithms.
                    // so if neither is a numeric literal, or if one or both are, if they are both integer literals
                    // in the range that we can EXACTLY represent them in a double, then we can proceed.
                    // NaN, +Infinity and -Infinity are also acceptable
                    if (left.IsOkayToCombine && right.IsOkayToCombine)
                    {
                        newLiteral = new JsConstantWrapper(left.ToString() + right.ToString(), JsPrimitiveType.String, null, m_parser);
                    }
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper Minus(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (left.IsOkayToCombine && right.IsOkayToCombine
                && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    double leftValue = left.ToNumber();
                    double rightValue = right.ToNumber();
                    double result = leftValue - rightValue;

                    if (JsConstantWrapper.NumberIsOkayToCombine(result))
                    {
                        newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                    }
                    else
                    {
                        if (!left.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(leftValue))
                        {
                            left.Parent.ReplaceChild(left, new JsConstantWrapper(leftValue, JsPrimitiveType.Number, left.Context, m_parser));
                        }
                        if (!right.IsNumericLiteral && JsConstantWrapper.NumberIsOkayToCombine(rightValue))
                        {
                            right.Parent.ReplaceChild(right, new JsConstantWrapper(rightValue, JsPrimitiveType.Number, right.Context, m_parser));
                        }
                    }
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper LeftShift(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    Int32 lvalue = left.ToInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue << rvalue);
                    newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }
            return newLiteral;
        }

        private JsConstantWrapper RightShift(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    Int32 lvalue = left.ToInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue >> rvalue);
                    newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper UnsignedRightShift(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // left-hand value is a 32-bit signed integer
                    UInt32 lvalue = left.ToUInt32();

                    // mask only the bottom 5 bits of the right-hand value
                    int rvalue = (int)(right.ToUInt32() & 0x1F);

                    // convert the result to a double
                    double result = Convert.ToDouble(lvalue >> rvalue);
                    newLiteral = new JsConstantWrapper(result, JsPrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper LessThan(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    if (left.IsOkayToCombine && right.IsOkayToCombine)
                    {
                        // do a straight ordinal comparison of the strings
                        newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) < 0, JsPrimitiveType.Boolean, null, m_parser);
                    }
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new JsConstantWrapper(left.ToNumber() < right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }
            return newLiteral;
        }

        private JsConstantWrapper LessThanOrEqual(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    if (left.IsOkayToCombine && right.IsOkayToCombine)
                    {
                        // do a straight ordinal comparison of the strings
                        newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) <= 0, JsPrimitiveType.Boolean, null, m_parser);
                    }
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new JsConstantWrapper(left.ToNumber() <= right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper GreaterThan(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    if (left.IsOkayToCombine && right.IsOkayToCombine)
                    {
                        // do a straight ordinal comparison of the strings
                        newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) > 0, JsPrimitiveType.Boolean, null, m_parser);
                    }
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new JsConstantWrapper(left.ToNumber() > right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper GreaterThanOrEqual(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                if (left.IsStringLiteral && right.IsStringLiteral)
                {
                    if (left.IsOkayToCombine && right.IsOkayToCombine)
                    {
                        // do a straight ordinal comparison of the strings
                        newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) >= 0, JsPrimitiveType.Boolean, null, m_parser);
                    }
                }
                else
                {
                    try
                    {
                        // either one or both are NOT a string -- numeric comparison
                        if (left.IsOkayToCombine && right.IsOkayToCombine)
                        {
                            newLiteral = new JsConstantWrapper(left.ToNumber() >= right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper Equal(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                JsPrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case JsPrimitiveType.Null:
                            // null == null is true
                            newLiteral = new JsConstantWrapper(true, JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new JsConstantWrapper(left.ToBoolean() == right.ToBoolean(), JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.String:
                            // compare string ordinally
                            if (left.IsOkayToCombine && right.IsOkayToCombine)
                            {
                                newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) == 0, JsPrimitiveType.Boolean, null, m_parser);
                            }
                            break;

                        case JsPrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new JsConstantWrapper(left.ToNumber() == right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else if (left.IsOkayToCombine && right.IsOkayToCombine)
                {
                    try
                    {
                        // numeric comparison
                        // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                        // and NaN is always unequal to everything else, including itself.
                        newLiteral = new JsConstantWrapper(left.ToNumber() == right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper NotEqual(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                JsPrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case JsPrimitiveType.Null:
                            // null != null is false
                            newLiteral = new JsConstantWrapper(false, JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new JsConstantWrapper(left.ToBoolean() != right.ToBoolean(), JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.String:
                            // compare string ordinally
                            if (left.IsOkayToCombine && right.IsOkayToCombine)
                            {
                                newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) != 0, JsPrimitiveType.Boolean, null, m_parser);
                            }
                            break;

                        case JsPrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new JsConstantWrapper(left.ToNumber() != right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else if (left.IsOkayToCombine && right.IsOkayToCombine)
                {
                    try
                    {
                        // numeric comparison
                        // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                        // and NaN is always unequal to everything else, including itself.
                        newLiteral = new JsConstantWrapper(left.ToNumber() != right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                    }
                    catch (InvalidCastException)
                    {
                        // some kind of casting in ToNumber caused a situation where we don't want
                        // to perform the combination on these operands
                    }
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper StrictEqual(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                JsPrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case JsPrimitiveType.Null:
                            // null === null is true
                            newLiteral = new JsConstantWrapper(true, JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new JsConstantWrapper(left.ToBoolean() == right.ToBoolean(), JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.String:
                            // compare string ordinally
                            if (left.IsOkayToCombine && right.IsOkayToCombine)
                            {
                                newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) == 0, JsPrimitiveType.Boolean, null, m_parser);
                            }
                            break;

                        case JsPrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new JsConstantWrapper(left.ToNumber() == right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else
                {
                    // if they aren't the same type, they ain't equal
                    newLiteral = new JsConstantWrapper(false, JsPrimitiveType.Boolean, null, m_parser);
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper StrictNotEqual(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                JsPrimitiveType leftType = left.PrimitiveType;
                if (leftType == right.PrimitiveType)
                {
                    // the values are the same type
                    switch (leftType)
                    {
                        case JsPrimitiveType.Null:
                            // null !== null is false
                            newLiteral = new JsConstantWrapper(false, JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.Boolean:
                            // compare boolean values
                            newLiteral = new JsConstantWrapper(left.ToBoolean() != right.ToBoolean(), JsPrimitiveType.Boolean, null, m_parser);
                            break;

                        case JsPrimitiveType.String:
                            // compare string ordinally
                            if (left.IsOkayToCombine && right.IsOkayToCombine)
                            {
                                newLiteral = new JsConstantWrapper(string.CompareOrdinal(left.ToString(), right.ToString()) != 0, JsPrimitiveType.Boolean, null, m_parser);
                            }
                            break;

                        case JsPrimitiveType.Number:
                            try
                            {
                                // compare the values
                                // +0 and -0 are treated as "equal" in C#, so we don't need to test them separately.
                                // and NaN is always unequal to everything else, including itself.
                                if (left.IsOkayToCombine && right.IsOkayToCombine)
                                {
                                    newLiteral = new JsConstantWrapper(left.ToNumber() != right.ToNumber(), JsPrimitiveType.Boolean, null, m_parser);
                                }
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                            break;
                    }
                }
                else
                {
                    // if they aren't the same type, they are not equal
                    newLiteral = new JsConstantWrapper(true, JsPrimitiveType.Boolean, null, m_parser);
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper BitwiseAnd(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new JsConstantWrapper(Convert.ToDouble(lValue & rValue), JsPrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper BitwiseOr(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new JsConstantWrapper(Convert.ToDouble(lValue | rValue), JsPrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper BitwiseXor(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;

            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    Int32 lValue = left.ToInt32();
                    Int32 rValue = right.ToInt32();
                    newLiteral = new JsConstantWrapper(Convert.ToDouble(lValue ^ rValue), JsPrimitiveType.Number, null, m_parser);
                }
                catch (InvalidCastException)
                {
                    // some kind of casting in ToNumber caused a situation where we don't want
                    // to perform the combination on these operands
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper LogicalAnd(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // if the left-hand side evaluates to true, return the right-hand side.
                    // if the left-hand side is false, return it.
                    newLiteral = left.ToBoolean() ? right : left;
                }
                catch (InvalidCastException)
                {
                    // if we couldn't cast to bool, ignore
                }
            }

            return newLiteral;
        }

        private JsConstantWrapper LogicalOr(JsConstantWrapper left, JsConstantWrapper right)
        {
            JsConstantWrapper newLiteral = null;
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                try
                {
                    // if the left-hand side evaluates to true, return the left-hand side.
                    // if the left-hand side is false, return the right-hand side.
                    newLiteral = left.ToBoolean() ? left : right;
                }
                catch (InvalidCastException)
                {
                    // if we couldn't cast to bool, ignore
                }
            }

            return newLiteral;
        }

        private static bool OnlyHasConstantItems(JsArrayLiteral arrayLiteral)
        {
            var elementCount = arrayLiteral.Elements.Count;
            for (var ndx = 0; ndx < elementCount; ++ndx)
            {
                // if any one element isn't a constant or isn't safe for combination, then bail with false
                var constantWrapper = arrayLiteral.Elements[ndx] as JsConstantWrapper;
                if (constantWrapper == null || !constantWrapper.IsOkayToCombine)
                {
                    return false;
                }
            }

            // if we get here, they were all constant
            return true;
        }

        private static string ComputeJoin(JsArrayLiteral arrayLiteral, JsConstantWrapper separatorNode)
        {
            // if the separator node is null, then the separator is a single comma character.
            // otherwise it's just the string value of the separator.
            var separator = separatorNode == null ? "," : separatorNode.ToString();

            var sb = new StringBuilder();
            for (var ndx = 0; ndx < arrayLiteral.Elements.Count; ++ndx)
            {
                // add the separator between items (if we have one)
                if (ndx > 0 && !string.IsNullOrEmpty(separator))
                {
                    sb.Append(separator);
                }

                // the element is a constant wrapper (we wouldn't get this far if it wasn't),
                // but we've overloaded the virtual ToString method on ConstantWrappers to convert the
                // constant value to a string value.
                sb.Append(arrayLiteral.Elements[ndx].ToString());
            }

            return sb.ToString();
        }

        #endregion

        //
        // IVisitor implementations
        //

        public override void Visit(JsAstNodeList node)
        {
            if (node != null)
            {
                var commaOperator = node.Parent as JsCommaOperator;
                JsAstNodeList list;
                if (commaOperator != null
                    && (list = commaOperator.Operand2 as JsAstNodeList) != null)
                {
                    // this list is part of a comma-operator, which is a collection of contiguous
                    // expression statements that we combined together. What we want to do is
                    // delete all constant elements from the list.
                    // if the parent is a block, then this was just a collection of statements and
                    // we can delete ALL constant expressions. But if the parent is not a block, then
                    // we will want to keep the last one as-is because it is the return value of the
                    // overall expression.
                    for (var ndx = list.Count - (node.Parent is JsBlock ? 1 : 2); ndx >= 0; --ndx)
                    {
                        if (list[ndx] is JsConstantWrapper)
                        {
                            list.RemoveAt(ndx);
                        }
                    }

                }

                // then normally recurse whatever is left over
                base.Visit(node);
            }
        }

        public override void Visit(JsBinaryOperator node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                // then evaluate.
                // do it in a separate method than this one because if this method
                // allocates a lot of bytes on the stack, we'll overflow our stack for
                // code that has lots of expression statements that get converted into one
                // BIG, uber-nested set of comma operators.
                DoBinaryOperator(node);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void DoBinaryOperator(JsBinaryOperator node)
        {
            if (m_parser.Settings.EvalLiteralExpressions)
            {
                // if this is an assign operator, an in, or an instanceof, then we won't
                // try to evaluate it
                if (!node.IsAssign && node.OperatorToken != JsToken.In && node.OperatorToken != JsToken.InstanceOf)
                {
                    if (node.OperatorToken == JsToken.StrictEqual || node.OperatorToken == JsToken.StrictNotEqual)
                    {
                        // the operator is a strict equality (or not-equal).
                        // check the primitive types of the two operands -- if they are known but not the same, we can
                        // shortcut the whole process by just replacing this node with a boolean literal.
                        var leftType = node.Operand1.FindPrimitiveType();
                        if (leftType != JsPrimitiveType.Other)
                        {
                            var rightType = node.Operand2.FindPrimitiveType();
                            if (rightType != JsPrimitiveType.Other)
                            {
                                // both sides are known
                                if (leftType != rightType)
                                {
                                    // they are not the same type -- replace with a boolean and bail
                                    ReplaceNodeWithLiteral(
                                        node, 
                                        new JsConstantWrapper(node.OperatorToken == JsToken.StrictEqual ? false : true, JsPrimitiveType.Boolean, node.Context, m_parser));
                                    return;
                                }

                                // they are the same type -- we can change the operator to simple equality/not equality
                                node.OperatorToken = node.OperatorToken == JsToken.StrictEqual ? JsToken.Equal : JsToken.NotEqual;
                            }
                        }
                    }

                    // see if the left operand is a literal number, boolean, string, or null
                    JsConstantWrapper left = node.Operand1 as JsConstantWrapper;
                    if (left != null)
                    {
                        if (node.OperatorToken == JsToken.Comma)
                        {
                            // the comma operator evaluates the left, then evaluates the right and returns it.
                            // but if the left is a literal, evaluating it doesn't DO anything, so we can replace the
                            // entire operation with the right-hand operand
                            JsConstantWrapper rightConstant = node.Operand2 as JsConstantWrapper;
                            if (rightConstant != null)
                            {
                                // we'll replace the operator with the right-hand operand, BUT it's a constant, too.
                                // first check to see if replacing this node with a constant will result in creating
                                // a member-bracket operator that can be turned into a member-dot. If it is, then that
                                // method will handle the replacement. But if it doesn't, then we should just replace
                                // the comma with the right-hand operand.
                                if (!ReplaceMemberBracketWithDot(node, rightConstant))
                                {
                                    ReplaceNodeWithLiteral(node, rightConstant);
                                }
                            }
                            else if (node is JsCommaOperator)
                            {
                                // this is a collection of expression statements that we've joined together as
                                // an extended comma operator. 
                                var list = node.Operand2 as JsAstNodeList;
                                if (list == null)
                                {
                                    // not a list, just a single item, so we can just
                                    // replace this entire node with the one element
                                    ReplaceNodeCheckParens(node, node.Operand2);
                                }
                                else if (list.Count == 1)
                                {
                                    // If the list has a single element, then we can just
                                    // replace this entire node with the one element
                                    ReplaceNodeCheckParens(node, list[0]);
                                }
                                else if (list.Count == 0)
                                {
                                    // the recursion ended up emptying the list, so we can just delete
                                    // this node altogether
                                    ReplaceNodeCheckParens(node, null);
                                }
                                else
                                {
                                    // more than one item in the list
                                    // move the first item from the list to the left-hand side
                                    var firstItem = list[0];
                                    list.RemoveAt(0);
                                    node.Operand1 = firstItem;

                                    // if there's only one item left in the list, we can get rid of the
                                    // extra list node and make it just the remaining node
                                    if (list.Count == 1)
                                    {
                                        firstItem = list[0];
                                        list.RemoveAt(0);
                                        node.Operand2 = firstItem;
                                    }
                                }
                            }
                            else
                            {
                                // replace the comma operator with the right-hand operand
                                ReplaceNodeCheckParens(node, node.Operand2);
                            }
                        }
                        else
                        {
                            // see if the right operand is a literal number, boolean, string, or null
                            JsConstantWrapper right = node.Operand2 as JsConstantWrapper;
                            if (right != null)
                            {
                                // then they are both constants and we can evaluate the operation
                                EvalThisOperator(node, left, right);
                            }
                            else
                            {
                                // see if the right is a binary operator that can be combined with ours
                                JsBinaryOperator rightBinary = node.Operand2 as JsBinaryOperator;
                                if (rightBinary != null)
                                {
                                    JsConstantWrapper rightLeft = rightBinary.Operand1 as JsConstantWrapper;
                                    if (rightLeft != null)
                                    {
                                        // eval our left and the right-hand binary's left and put the combined operation as
                                        // the child of the right-hand binary
                                        EvalToTheRight(node, left, rightLeft, rightBinary);
                                    }
                                    else
                                    {
                                        JsConstantWrapper rightRight = rightBinary.Operand2 as JsConstantWrapper;
                                        if (rightRight != null)
                                        {
                                            EvalFarToTheRight(node, left, rightRight, rightBinary);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // left is not a constantwrapper. See if the right is
                        JsConstantWrapper right = node.Operand2 as JsConstantWrapper;
                        if (right != null)
                        {
                            // the right is a constant. See if the the left is a binary operator...
                            JsBinaryOperator leftBinary = node.Operand1 as JsBinaryOperator;
                            if (leftBinary != null)
                            {
                                // ...with a constant on the right, and the operators can be combined
                                JsConstantWrapper leftRight = leftBinary.Operand2 as JsConstantWrapper;
                                if (leftRight != null)
                                {
                                    EvalToTheLeft(node, right, leftRight, leftBinary);
                                }
                                else
                                {
                                    JsConstantWrapper leftLeft = leftBinary.Operand1 as JsConstantWrapper;
                                    if (leftLeft != null)
                                    {
                                        EvalFarToTheLeft(node, right, leftLeft, leftBinary);
                                    }
                                }
                            }
                            else if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.SimplifyStringToNumericConversion))
                            {
                                // see if it's a lookup and this is a minus operation and the constant is a zero
                                JsLookup lookup = node.Operand1 as JsLookup;
                                if (lookup != null && node.OperatorToken == JsToken.Minus && right.IsIntegerLiteral && right.ToNumber() == 0)
                                {
                                    // okay, so we have "lookup - 0"
                                    // this is done frequently to force a value to be numeric. 
                                    // There is an easier way: apply the unary + operator to it. 
                                    var unary = new JsUnaryOperator(node.Context, m_parser)
                                        {
                                            Operand = lookup,
                                            OperatorToken = JsToken.Plus
                                        };
                                    ReplaceNodeCheckParens(node, unary);
                                }
                            }
                        }
                        // TODO: shouldn't we check if they BOTH are binary operators? (a*6)*(5*b) ==> a*30*b (for instance)
                    }
                }
            }
        }

        public override void Visit(JsCallNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                // if this isn't a constructor and it isn't a member-brackets operator
                if (!node.IsConstructor && !node.InBrackets)
                {
                    // check to see if this is a call of certain member functions
                    var member = node.Function as JsMember;
                    if (member != null)
                    {
                        if (string.CompareOrdinal(member.Name, "join") == 0 && node.Arguments.Count <= 1
                            && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateLiteralJoins))
                        {
                            // this is a call to join with zero or one argument (no more)
                            // see if the root is an array literal that has no issues
                            var arrayLiteral = member.Root as JsArrayLiteral;
                            if (arrayLiteral != null && !arrayLiteral.MayHaveIssues)
                            {
                                // it is -- make sure the separator is either not specified or is a constant
                                JsConstantWrapper separator = null;
                                if (node.Arguments.Count == 0 || (separator = node.Arguments[0] as JsConstantWrapper) != null)
                                {
                                    // and the array literal must contain only constant items
                                    if (OnlyHasConstantItems(arrayLiteral))
                                    {
                                        // last test: compute the combined string and only use it if it's actually
                                        // shorter than the original code
                                        var combinedJoin = ComputeJoin(arrayLiteral, separator);
                                        if (combinedJoin.Length + 2 < node.ToCode().Length)
                                        {
                                            // transform: [c,c,c].join(s) => "cscsc"
                                            ReplaceNodeWithLiteral(node, 
                                                new JsConstantWrapper(combinedJoin, JsPrimitiveType.String, node.Context, node.Parser));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void Visit(JsConditional node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoConditional(node);
            }
        }

        private void DoConditional(JsConditional node)
        {
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                // if the condition is a literal, evaluating the condition doesn't do anything, AND
                // we know now whether it's true or not.
                JsConstantWrapper literalCondition = node.Condition as JsConstantWrapper;
                if (literalCondition != null)
                {
                    try
                    {
                        // if the boolean represenation of the literal is true, we can replace the condition operator
                        // with the true expression; otherwise we can replace it with the false expression
                        ReplaceNodeCheckParens(node, literalCondition.ToBoolean() ? node.TrueExpression : node.FalseExpression);
                    }
                    catch (InvalidCastException)
                    {
                        // ignore any invalid cast errors
                    }
                }
            }
        }

        public override void Visit(JsConditionalCompilationElseIf node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoConditionalCompilationElseIf(node);
            }
        }

        private void DoConditionalCompilationElseIf(JsConditionalCompilationElseIf node)
        {
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                // if the if-condition is a constant, we can eliminate one of the two branches
                JsConstantWrapper constantCondition = node.Condition as JsConstantWrapper;
                if (constantCondition != null)
                {
                    // instead, replace the condition with a 1 if it's always true or a 0 if it's always false
                    if (constantCondition.IsNotOneOrPositiveZero)
                    {
                        try
                        {
                            node.Condition =
                                new JsConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, JsPrimitiveType.Number, node.Condition.Context, m_parser);
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast exceptions
                        }
                    }
                }
            }
        }

        public override void Visit(JsConditionalCompilationIf node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoConditionalCompilationIf(node);
            }
        }

        private void DoConditionalCompilationIf(JsConditionalCompilationIf node)
        {
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                // if the if-condition is a constant, we can eliminate one of the two branches
                JsConstantWrapper constantCondition = node.Condition as JsConstantWrapper;
                if (constantCondition != null)
                {
                    // instead, replace the condition with a 1 if it's always true or a 0 if it's always false
                    if (constantCondition.IsNotOneOrPositiveZero)
                    {
                        try
                        {
                            node.Condition =
                                new JsConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, JsPrimitiveType.Number, node.Condition.Context, m_parser);
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast exceptions
                        }
                    }
                }
            }
        }

        public override void Visit(JsDoWhile node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoDoWhile(node);
            }
        }

        private void DoDoWhile(JsDoWhile node)
        {
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                // if the condition is a constant, we can simplify things
                JsConstantWrapper constantCondition = node.Condition as JsConstantWrapper;
                if (constantCondition != null && constantCondition.IsNotOneOrPositiveZero)
                {
                    try
                    {
                        // the condition is a constant, so it is always either true or false
                        // we can replace the condition with a one or a zero -- only one byte
                        node.Condition =
                            new JsConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, JsPrimitiveType.Number, node.Condition.Context, m_parser);
                    }
                    catch (InvalidCastException)
                    {
                        // ignore any invalid cast errors
                    }
                }
            }
        }

        public override void Visit(JsForNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoForNode(node);
            }
        }

        private void DoForNode(JsForNode node)
        {
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                JsConstantWrapper constantCondition = node.Condition as JsConstantWrapper;
                if (constantCondition != null)
                {
                    try
                    {
                        // if condition is always false, change it to a zero (only one byte)
                        // and if it is always true, remove it (default behavior)
                        if (constantCondition.ToBoolean())
                        {
                            // always true -- don't need a condition at all
                            node.Condition = null;
                        }
                        else if (constantCondition.IsNotOneOrPositiveZero)
                        {
                            // always false and it's not already a zero. Make it so (only one byte)
                            node.Condition = new JsConstantWrapper(0, JsPrimitiveType.Number, node.Condition.Context, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // ignore any invalid cast exceptions
                    }
                }
            }
        }

        public override void Visit(JsIfNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoIfNode(node);
            }
        }

        private void DoIfNode(JsIfNode node)
        {
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                // if the if-condition is a constant, we can eliminate one of the two branches
                JsConstantWrapper constantCondition = node.Condition as JsConstantWrapper;
                if (constantCondition != null)
                {
                    // instead, replace the condition with a 1 if it's always true or a 0 if it's always false
                    if (constantCondition.IsNotOneOrPositiveZero)
                    {
                        try
                        {
                            node.Condition =
                                new JsConstantWrapper(constantCondition.ToBoolean() ? 1 : 0, JsPrimitiveType.Number, node.Condition.Context, m_parser);
                        }
                        catch (InvalidCastException)
                        {
                            // ignore any invalid cast exceptions
                        }
                    }
                }
            }
        }

        public override void Visit(JsMember node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                if (string.CompareOrdinal(node.Name, "length") == 0
                    && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateLiteralLengths))
                {
                    // if we create a constant, we'll replace the current node with it
                    JsConstantWrapper length = null;

                    JsArrayLiteral arrayLiteral;
                    var constantWrapper = node.Root as JsConstantWrapper;
                    if (constantWrapper != null)
                    {
                        if (constantWrapper.PrimitiveType == JsPrimitiveType.String && !constantWrapper.MayHaveIssues)
                        {
                            length = new JsConstantWrapper(constantWrapper.ToString().Length, JsPrimitiveType.Number, node.Context, node.Parser);
                        }
                    }
                    else if ((arrayLiteral = node.Root as JsArrayLiteral) != null && !arrayLiteral.MayHaveIssues)
                    {
                        // get the count of items in the array literal, create a constant wrapper from it, and
                        // replace this node with it
                        length = new JsConstantWrapper(arrayLiteral.Elements.Count, JsPrimitiveType.Number, node.Context, node.Parser);
                    }

                    if (length != null)
                    {
                        node.Parent.ReplaceChild(node, length);
                    }
                }
            }
        }

        public override void Visit(JsUnaryOperator node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoUnaryNode(node);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private void DoUnaryNode(JsUnaryOperator node)
        {
            if (!node.OperatorInConditionalCompilationComment
                && m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                var literalOperand = node.Operand as JsConstantWrapper;
                switch(node.OperatorToken)
                {
                    case JsToken.Void:
                        // see if our operand is a ConstantWrapper
                        if (literalOperand != null)
                        {
                            // either number, string, boolean, or null.
                            // the void operator evaluates its operand and returns undefined. Since evaluating a literal
                            // does nothing, then it doesn't matter what the heck it is. Replace it with a zero -- a one-
                            // character literal.
                            node.Operand = new JsConstantWrapper(0, JsPrimitiveType.Number, node.Context, m_parser);
                        }
                        break;

                    case JsToken.TypeOf:
                        if (literalOperand != null)
                        {
                            // either number, string, boolean, or null.
                            // the operand is a literal. Therefore we already know what the typeof
                            // operator will return. Just short-circuit that behavior now and replace the operator
                            // with a string literal of the proper value
                            string typeName = null;
                            if (literalOperand.IsStringLiteral)
                            {
                                // "string"
                                typeName = "string";
                            }
                            else if (literalOperand.IsNumericLiteral)
                            {
                                // "number"
                                typeName = "number";
                            }
                            else if (literalOperand.IsBooleanLiteral)
                            {
                                // "boolean"
                                typeName = "boolean";
                            }
                            else if (literalOperand.Value == null)
                            {
                                // "object"
                                typeName = "object";
                            }

                            if (!string.IsNullOrEmpty(typeName))
                            {
                                ReplaceNodeWithLiteral(node, new JsConstantWrapper(typeName, JsPrimitiveType.String, node.Context, m_parser));
                            }
                        }
                        else if (node.Operand is JsObjectLiteral)
                        {
                            ReplaceNodeWithLiteral(node, new JsConstantWrapper("object", JsPrimitiveType.String, node.Context, m_parser));
                        }
                        break;

                    case JsToken.Plus:
                        if (literalOperand != null)
                        {
                            try
                            {
                                // replace with a constant representing operand.ToNumber,
                                ReplaceNodeWithLiteral(node, new JsConstantWrapper(literalOperand.ToNumber(), JsPrimitiveType.Number, node.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                        }
                        break;

                    case JsToken.Minus:
                        if (literalOperand != null)
                        {
                            try
                            {
                                // replace with a constant representing the negative of operand.ToNumber
                                ReplaceNodeWithLiteral(node, new JsConstantWrapper(-literalOperand.ToNumber(), JsPrimitiveType.Number, node.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                        }
                        break;

                    case JsToken.BitwiseNot:
                        if (literalOperand != null)
                        {
                            try
                            {
                                // replace with a constant representing the bitwise-not of operant.ToInt32
                                ReplaceNodeWithLiteral(node, new JsConstantWrapper(Convert.ToDouble(~literalOperand.ToInt32()), JsPrimitiveType.Number, node.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // some kind of casting in ToNumber caused a situation where we don't want
                                // to perform the combination on these operands
                            }
                        }
                        break;

                    case JsToken.LogicalNot:
                        if (literalOperand != null)
                        {
                            // replace with a constant representing the opposite of operand.ToBoolean
                            try
                            {
                                ReplaceNodeWithLiteral(node, new JsConstantWrapper(!literalOperand.ToBoolean(), JsPrimitiveType.Boolean, node.Context, m_parser));
                            }
                            catch (InvalidCastException)
                            {
                                // ignore any invalid cast exceptions
                            }
                        }
                        break;
                }
            }
        }

        public override void Visit(JsWhileNode node)
        {
            if (node != null)
            {
                // depth-first
                base.Visit(node);

                DoWhileNode(node);
            }
        }

        private void DoWhileNode(JsWhileNode node)
        {
            // see if the condition is a constant
            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.EvaluateNumericExpressions))
            {
                var constantCondition = node.Condition as JsConstantWrapper;
                if (constantCondition != null)
                {
                    // TODO: (someday) we'd RATHER eliminate the statement altogether if the condition is always false,
                    // but we'd need to make sure var'd variables and declared functions are properly handled.
                    try
                    {
                        bool isTrue = constantCondition.ToBoolean();
                        if (isTrue)
                        {
                            // the condition is always true, so we should change it to 1
                            if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.ChangeWhileToFor))
                            {
                                // the condition is always true; we should change it to a for(;;) statement.
                                // less bytes than while(1)

                                // check to see if we want to combine a preceding var with a for-statement
                                JsAstNode initializer = null;
                                if (m_parser.Settings.IsModificationAllowed(JsTreeModifications.MoveVarIntoFor))
                                {
                                    // if the previous statement is a var, we can move it to the initializer
                                    // and save even more bytes. The parent should always be a block. If not, 
                                    // then assume there is no previous.
                                    var parentBlock = node.Parent as JsBlock;
                                    if (parentBlock != null)
                                    {
                                        int whileIndex = parentBlock.IndexOf(node);
                                        if (whileIndex > 0)
                                        {
                                            var previousVar = parentBlock[whileIndex - 1] as JsVar;
                                            if (previousVar != null)
                                            {
                                                initializer = previousVar;
                                                parentBlock.RemoveAt(whileIndex - 1);
                                            }
                                        }
                                    }
                                }

                                // create the for using our body and replace ourselves with it
                                var forNode = new JsForNode(node.Context, m_parser)
                                    {
                                        Initializer = initializer,
                                        Body = node.Body
                                    };
                                node.Parent.ReplaceChild(node, forNode);
                            }
                            else
                            {
                                // the condition is always true, so we can replace the condition
                                // with a 1 -- only one byte
                                node.Condition = new JsConstantWrapper(1, JsPrimitiveType.Number, null, m_parser);
                            }
                        }
                        else if (constantCondition.IsNotOneOrPositiveZero)
                        {
                            // the condition is always false, so we can replace the condition
                            // with a zero -- only one byte
                            node.Condition = new JsConstantWrapper(0, JsPrimitiveType.Number, null, m_parser);
                        }
                    }
                    catch (InvalidCastException)
                    {
                        // ignore any invalid cast exceptions
                    }
                }
            }
        }
    }
}
