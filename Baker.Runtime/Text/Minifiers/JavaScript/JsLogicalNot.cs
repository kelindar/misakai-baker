using System;
using System.Collections.Generic;
using System.Text;

namespace Baker.Text
{
    internal class JsLogicalNot : JsTreeVisitor
    {
        private JsAstNode m_expression;
        private bool m_measure;
        private JsParser m_parser;
        private int m_delta;

        public JsLogicalNot(JsAstNode node, JsParser parser)
        {
            m_expression = node;
            m_parser = parser;
        }

        public int Measure()
        {
            // we just want to measure the potential delta
            m_measure = true;
            m_delta = 0;

            // do it and return the result
            m_expression.Accept(this);
            return m_delta;
        }

        public void Apply()
        {
            // not measuring; doing
            m_measure = false;
            m_expression.Accept(this);
        }

        public static void Apply(JsAstNode node, JsParser parser)
        {
            var logicalNot = new JsLogicalNot(node, parser);
            logicalNot.Apply();
        }

        private void WrapWithLogicalNot(JsAstNode operand)
        {
            operand.Parent.ReplaceChild(
                operand,
                new JsUnaryOperator(operand.Context, m_parser)
                    {
                        Operand = operand,
                        OperatorToken = JsToken.LogicalNot
                    });
        }

        private void TypicalHandler(JsAstNode node)
        {
            if (node != null)
            {
                // don't need to recurse -- to logical-not this, we just need to apply
                // the logical-not operator to it, which will add a character
                if (m_measure)
                {
                    // measure
                    ++m_delta;
                }
                else
                {
                    // simple convert
                    WrapWithLogicalNot(node);
                }
            }
        }

        public override void Visit(JsAstNodeList node)
        {
            if (node != null && node.Count > 0)
            {
                // this is really only ever not-ed when it's the right-hand operand
                // of a comma operator, which we flattened to decrease stack recursion.
                // so to logical-not this element, we only need to not the last item
                // in the list (because all the others are comma-separated)
                node[node.Count - 1].Accept(this);
            }
        }

        public override void Visit(JsArrayLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsBinaryOperator node)
        {
            if (node != null)
            {
                if (m_measure)
                {
                    // measure
                    MeasureBinaryOperator(node);
                }
                else
                {
                    // convert
                    ConvertBinaryOperator(node);
                }
            }
        }

        private void MeasureBinaryOperator(JsBinaryOperator node)
        {
            // depending on the operator, calculate the potential difference in length
            switch (node.OperatorToken)
            {
                case JsToken.Equal:
                case JsToken.NotEqual:
                case JsToken.StrictEqual:
                case JsToken.StrictNotEqual:
                    // these operators can be turned into a logical not without any
                    // delta in code size. == becomes !=, etc.
                    break;

                case JsToken.LessThan:
                case JsToken.GreaterThan:
                // these operators would add another character when turnbed into a not.
                // for example, < becomes >=, etc
                //++m_delta;
                //break;

                case JsToken.LessThanEqual:
                case JsToken.GreaterThanEqual:
                // these operators would subtract another character when turnbed into a not.
                // for example, <= becomes >, etc
                //--m_delta;
                //break;

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
                case JsToken.BitwiseAnd:
                case JsToken.BitwiseOr:
                case JsToken.BitwiseXor:
                case JsToken.Divide:
                case JsToken.Multiply:
                case JsToken.Modulo:
                case JsToken.Minus:
                case JsToken.Plus:
                case JsToken.LeftShift:
                case JsToken.RightShift:
                case JsToken.UnsignedRightShift:
                case JsToken.In:
                case JsToken.InstanceOf:
                    // these operators have no logical not, which means we need to wrap them in
                    // a unary logical-not operator. And since they have a lower precedence than
                    // the unary logical-not, they'll have to be wrapped in parens. So that means
                    // logical-not'ing these guys adds three characters
                    m_delta += 3;
                    break;

                case JsToken.Comma:
                    // to logical-not a comma-operator, we just need to logical-not the
                    // right-hand side
                    node.Operand2.Accept(this);
                    break;

                case JsToken.LogicalAnd:
                case JsToken.LogicalOr:
                    if (node.Parent is JsBlock || (node.Parent is JsCommaOperator && node.Parent.Parent is JsBlock))
                    {
                        // if the parent is a block, then this is a simple expression statement:
                        // expr1 || expr2; or expr1 && expr2; If so, then the result isn't
                        // used anywhere and we're just using the || or && operator as a
                        // shorter if-statement. So we don't need to negate the right-hand
                        // side, just the left-hand side.
                        if (node.Operand1 != null)
                        {
                            node.Operand1.Accept(this);
                        }
                    }
                    else
                    {
                        // the logical-not of a logical-and or logical-or operation is the 
                        // other operation against the not of each operand. Since the opposite
                        // operator is the same length as this operator, then we just need
                        // to recurse both operands to find the true delta.
                        if (node.Operand1 != null)
                        {
                            node.Operand1.Accept(this);
                        }

                        if (node.Operand2 != null)
                        {
                            node.Operand2.Accept(this);
                        }
                    }
                    break;
            }
        }

        private void ConvertBinaryOperator(JsBinaryOperator node)
        {
            // depending on the operator, perform whatever we need to do to apply a logical
            // not to the operation
            switch (node.OperatorToken)
            {
                case JsToken.Equal:
                    node.OperatorToken = JsToken.NotEqual;
                    break;

                case JsToken.NotEqual:
                    node.OperatorToken = JsToken.Equal;
                    break;

                case JsToken.StrictEqual:
                    node.OperatorToken = JsToken.StrictNotEqual;
                    break;

                case JsToken.StrictNotEqual:
                    node.OperatorToken = JsToken.StrictEqual;
                    break;

                case JsToken.LessThan:
                //node.OperatorToken = JSToken.GreaterThanEqual;
                //break;

                case JsToken.GreaterThan:
                //node.OperatorToken = JSToken.LessThanEqual;
                //break;

                case JsToken.LessThanEqual:
                //node.OperatorToken = JSToken.GreaterThan;
                //break;

                case JsToken.GreaterThanEqual:
                //node.OperatorToken = JSToken.LessThan;
                //break;

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
                case JsToken.BitwiseAnd:
                case JsToken.BitwiseOr:
                case JsToken.BitwiseXor:
                case JsToken.Divide:
                case JsToken.Multiply:
                case JsToken.Modulo:
                case JsToken.Minus:
                case JsToken.Plus:
                case JsToken.LeftShift:
                case JsToken.RightShift:
                case JsToken.UnsignedRightShift:
                case JsToken.In:
                case JsToken.InstanceOf:
                    WrapWithLogicalNot(node);
                    break;

                case JsToken.Comma:
                    // to logical-not a comma-operator, we just need to logical-not the
                    // right-hand side
                    node.Operand2.Accept(this);
                    break;

                case JsToken.LogicalAnd:
                case JsToken.LogicalOr:
                    if (node.Parent is JsBlock || (node.Parent is JsCommaOperator && node.Parent.Parent is JsBlock))
                    {
                        // if the parent is a block, then this is a simple expression statement:
                        // expr1 || expr2; or expr1 && expr2; If so, then the result isn't
                        // used anywhere and we're just using the || or && operator as a
                        // shorter if-statement. So we don't need to negate the right-hand
                        // side, just the left-hand side.
                        if (node.Operand1 != null)
                        {
                            node.Operand1.Accept(this);
                        }
                    }
                    else
                    {
                        // the logical-not of a logical-and or logical-or operation is the 
                        // other operation against the not of each operand. Since the opposite
                        // operator is the same length as this operator, then we just need
                        // to recurse both operands and swap the operator token
                        if (node.Operand1 != null)
                        {
                            node.Operand1.Accept(this);
                        }

                        if (node.Operand2 != null)
                        {
                            node.Operand2.Accept(this);
                        }
                    }
                    node.OperatorToken = node.OperatorToken == JsToken.LogicalAnd ? JsToken.LogicalOr : JsToken.LogicalAnd;
                    break;
            }
        }

        public override void Visit(JsCallNode node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsConditional node)
        {
            if (node != null)
            {
                // we have two choices for the conditional. Either:
                //  1. we wrap the whole thing in a logical-not operator, which means we also need to
                //     add parentheses, since conditional is lower-precedence than the logicial not, or
                //  2. apply the logical-not to both the true- and false-branches.
                // The first is guaranteed 3 additional characters. We have to check the delta for
                // each branch and add them together to know how much the second would cost. If it's 
                // greater than 3, then we just want to not the whole thing.
                var notTrue = new JsLogicalNot(node.TrueExpression, m_parser);
                var notFalse = new JsLogicalNot(node.FalseExpression, m_parser);
                var costNottingBoth = notTrue.Measure() + notFalse.Measure();

                if (m_measure)
                {
                    // we're just measuring -- adjust the delta accordingly
                    // (the lesser of the two options)
                    m_delta += (costNottingBoth > 3) ? 3 : costNottingBoth;
                }
                else if (costNottingBoth > 3)
                {
                    // just wrap the whole thing
                    WrapWithLogicalNot(node);
                }
                else
                {
                    // less bytes to wrap each branch separately
                    node.TrueExpression.Accept(this);
                    node.FalseExpression.Accept(this);
                }
            }
        }

        public override void Visit(JsConstantWrapper node)
        {
            if (node != null)
            {
                // measure
                if (node.PrimitiveType == JsPrimitiveType.Boolean)
                {
                    if (m_measure)
                    {
                        // if we are converting true/false literals to !0/!1, then
                        // a logical-not doesn't add or subtract anything. But if we aren't,
                        // we need to add/subtract the difference in the length between the
                        // "true" and "false" strings
                        if (!m_parser.Settings.MinifyCode
                            || !m_parser.Settings.IsModificationAllowed(JsTreeModifications.BooleanLiteralsToNotOperators))
                        {
                            // converting true to false adds a character, false to true subtracts
                            m_delta += node.ToBoolean() ? 1 : -1;
                        }
                    }
                    else
                    {
                        // convert - just flip the boolean value
                        node.Value = !node.ToBoolean();
                    }
                }
                else
                {
                    // just the same typical operation as most other nodes for other types
                    TypicalHandler(node);
                }
            }
        }

        public override void Visit(JsGroupingOperator node)
        {
            if (node != null)
            {
                if (m_measure)
                {
                    // either we add one by putting a ! in front of the parens, or
                    // the expression itself can come out equal to less.
                    // save the current delta, check the operand, and if applying the
                    // logical not to the operand is MORE than just throwing a ! on the
                    // front, then we'll just return the +1 for the simple not.
                    var plusOne = m_delta + 1;
                    node.Operand.Accept(this);
                    if (m_delta > plusOne)
                    {
                        m_delta = plusOne;
                    }
                }
                else
                {
                    // we need to know how we're going to do this, so we need
                    // to run another measurement.
                    m_measure = true;
                    m_delta = 0;
                    node.Operand.Accept(this);
                    m_measure = false;

                    // if the delta is greater than 1, then we are just going
                    // to wrap ourselves in a unary not. Otherwise we're going
                    // to replace ourselves with our operand and not it in-place.
                    if (m_delta > 1)
                    {
                        WrapWithLogicalNot(node);
                    }
                    else
                    {
                        node.Parent.ReplaceChild(node, node.Operand);
                        node.Operand.Accept(this);
                    }
                }
            }
        }

        public override void Visit(JsLookup node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsMember node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsObjectLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsRegExpLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsThisLiteral node)
        {
            // same logic for most nodes
            TypicalHandler(node);
        }

        public override void Visit(JsUnaryOperator node)
        {
            if (node != null && !node.OperatorInConditionalCompilationComment)
            {
                // if this is a unary logical-not operator, then we will just remove the
                // logical-not operation
                if (node.OperatorToken == JsToken.LogicalNot)
                {
                    if (m_measure)
                    {
                        // measure
                        // removes the not operator character, but also might remove parens that we would
                        // no longer need.
                        --m_delta;
                        if (node.Operand is JsBinaryOperator || node.Operand is JsConditional || node.Operand is JsGroupingOperator)
                        {
                            // those operators are lesser-precedence than the logical-not coperator and would've
                            // added parens that we now don't need
                            m_delta -= 2;
                        }
                    }
                    else
                    {
                        // convert
                        // just replace the not with its own operand, unless the child
                        // itself is a grouping operator, in which case we will replace it
                        // with the grouping operand to get rid of the parens
                        var grouping = node.Operand as JsGroupingOperator;
                        if (grouping != null)
                        {
                            node.Parent.ReplaceChild(node, grouping.Operand);
                        }
                        else
                        {
                            node.Parent.ReplaceChild(node, node.Operand);
                        }
                    }
                }
                else
                {
                    // same logic as most nodes for the other operators
                    TypicalHandler(node);
                }
            }
        }
    }
}
