// TreeVisitor.cs
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

namespace Baker.Text
{
    internal class JsTreeVisitor : IJsVisitor
    {
        public JsTreeVisitor() { }

        #region IVisitor Members

        public virtual void Visit(JsArrayLiteral node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsAspNetBlockNode node)
        {
            // no children
        }

        public virtual void Visit(JsAstNodeList node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsBinaryOperator node)
        {
            if (node != null)
            {
                if (node.Operand1 != null)
                {
                    node.Operand1.Accept(this);
                }

                if (node.Operand2 != null)
                {
                    node.Operand2.Accept(this);
                }
            }
        }

        public virtual void Visit(JsBlock node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsBreak node)
        {
            if (node != null)
            {
                // no children
            }
        }

        public virtual void Visit(JsCallNode node)
        {
            if (node != null)
            {
                if (node.Function != null)
                {
                    node.Function.Accept(this);
                }

                if (node.Arguments != null)
                {
                    node.Arguments.Accept(this);
                }
            }
        }

        public virtual void Visit(JsConditionalCompilationComment node)
        {
            if (node != null)
            {
                if (node.Statements != null)
                {
                    node.Statements.Accept(this);
                }
            }
        }

        public virtual void Visit(JsConditionalCompilationElse node)
        {
            // no children
        }

        public virtual void Visit(JsConditionalCompilationElseIf node)
        {
            if (node != null)
            {
                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }
            }
        }

        public virtual void Visit(JsConditionalCompilationEnd node)
        {
            // no children
        }

        public virtual void Visit(JsConditionalCompilationIf node)
        {
            if (node != null)
            {
                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }
            }
        }

        public virtual void Visit(JsConditionalCompilationOn node)
        {
            // no children
        }

        public virtual void Visit(JsConditionalCompilationSet node)
        {
            if (node != null)
            {
                if (node.Value != null)
                {
                    node.Value.Accept(this);
                }
            }
        }

        public virtual void Visit(JsConditional node)
        {
            if (node != null)
            {
                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }

                if (node.TrueExpression != null)
                {
                    node.TrueExpression.Accept(this);
                }

                if (node.FalseExpression != null)
                {
                    node.FalseExpression.Accept(this);
                }
            }
        }

        public virtual void Visit(JsConstantWrapper node)
        {
            // no children
        }

        public virtual void Visit(JsConstantWrapperPP node)
        {
            // no children
        }

        public virtual void Visit(JsConstStatement node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsContinueNode node)
        {
            if (node != null)
            {
                // no children
            }
        }

        public virtual void Visit(JsCustomNode node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsDebuggerNode node)
        {
            // no children
        }

        public virtual void Visit(JsDirectivePrologue node)
        {
            // no children
        }

        public virtual void Visit(JsDoWhile node)
        {
            if (node != null)
            {
                if (node.Body != null)
                {
                    node.Body.Accept(this);
                }

                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }
            }
        }

        public virtual void Visit(JsEmptyStatement node)
        {
            // no children
        }

        public virtual void Visit(JsForIn node)
        {
            if (node != null)
            {
                if (node.Variable != null)
                {
                    node.Variable.Accept(this);
                }

                if (node.Collection != null)
                {
                    node.Collection.Accept(this);
                }

                if (node.Body != null)
                {
                    node.Body.Accept(this);
                }
            }
        }

        public virtual void Visit(JsForNode node)
        {
            if (node != null)
            {
                if (node.Initializer != null)
                {
                    node.Initializer.Accept(this);
                }

                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }

                if (node.Incrementer != null)
                {
                    node.Incrementer.Accept(this);
                }

                if (node.Body != null)
                {
                    node.Body.Accept(this);
                }
            }
        }

        public virtual void Visit(JsFunctionObject node)
        {
            if (node != null)
            {
                if (node.Body != null)
                {
                    node.Body.Accept(this);
                }
            }
        }

        public virtual void Visit(JsGetterSetter node)
        {
            // no children
        }

        public virtual void Visit(JsGroupingOperator node)
        {
            if (node != null && node.Operand != null)
            {
                node.Operand.Accept(this);
            }
        }

        public virtual void Visit(JsIfNode node)
        {
            if (node != null)
            {
                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }

                if (node.TrueBlock != null)
                {
                    node.TrueBlock.Accept(this);
                }

                if (node.FalseBlock != null)
                {
                    node.FalseBlock.Accept(this);
                }
            }
        }

        public virtual void Visit(JsImportantComment node)
        {
            // no children
        }

        public virtual void Visit(JsLabeledStatement node)
        {
            if (node != null)
            {
                if (node.Statement != null)
                {
                    node.Statement.Accept(this);
                }
            }
        }

        public virtual void Visit(JsLexicalDeclaration node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsLookup node)
        {
            // no children
        }

        public virtual void Visit(JsMember node)
        {
            if (node != null)
            {
                if (node.Root != null)
                {
                    node.Root.Accept(this);
                }
            }
        }

        public virtual void Visit(JsObjectLiteral node)
        {
            if (node != null)
            {
                if (node.Properties != null)
                {
                    node.Properties.Accept(this);
                }
            }
        }

        public virtual void Visit(JsObjectLiteralField node)
        {
            // no children
        }

        public virtual void Visit(JsObjectLiteralProperty node)
        {
            if (node != null)
            {
                if (node.Name != null)
                {
                    node.Name.Accept(this);
                }

                if (node.Value != null)
                {
                    node.Value.Accept(this);
                }
            }
        }

        public virtual void Visit(JsParameterDeclaration node)
        {
            // no children
        }

        public virtual void Visit(JsRegExpLiteral node)
        {
            // no children
        }

        public virtual void Visit(JsReturnNode node)
        {
            if (node != null)
            {
                if (node.Operand != null)
                {
                    node.Operand.Accept(this);
                }
            }
        }

        public virtual void Visit(JsSwitch node)
        {
            if (node != null)
            {
                if (node.Expression != null)
                {
                    node.Expression.Accept(this);
                }

                if (node.Cases != null)
                {
                    node.Cases.Accept(this);
                }
            }
        }

        public virtual void Visit(JsSwitchCase node)
        {
            if (node != null)
            {
                if (node.CaseValue != null)
                {
                    node.CaseValue.Accept(this);
                }

                if (node.Statements != null)
                {
                    node.Statements.Accept(this);
                }
            }
        }

        public virtual void Visit(JsThisLiteral node)
        {
            // no children
        }

        public virtual void Visit(JsThrowNode node)
        {
            if (node != null)
            {
                if (node.Operand != null)
                {
                    node.Operand.Accept(this);
                }
            }
        }

        public virtual void Visit(JsTryNode node)
        {
            if (node != null)
            {
                if (node.TryBlock != null)
                {
                    node.TryBlock.Accept(this);
                }

                if (node.CatchBlock != null)
                {
                    node.CatchBlock.Accept(this);
                }

                if (node.FinallyBlock != null)
                {
                    node.FinallyBlock.Accept(this);
                }
            }
        }

        public virtual void Visit(JsVar node)
        {
            if (node != null)
            {
                foreach (var childNode in node.Children)
                {
                    childNode.Accept(this);
                }
            }
        }

        public virtual void Visit(JsVariableDeclaration node)
        {
            if (node != null)
            {
                if (node.Initializer != null)
                {
                    node.Initializer.Accept(this);
                }
            }
        }

        public virtual void Visit(JsUnaryOperator node)
        {
            if (node != null)
            {
                if (node.Operand != null)
                {
                    node.Operand.Accept(this);
                }
            }
        }

        public virtual void Visit(JsWhileNode node)
        {
            if (node != null)
            {
                if (node.Condition != null)
                {
                    node.Condition.Accept(this);
                }

                if (node.Body != null)
                {
                    node.Body.Accept(this);
                }
            }
        }

        public virtual void Visit(JsWithNode node)
        {
            if (node != null)
            {
                if (node.WithObject != null)
                {
                    node.WithObject.Accept(this);
                }

                if (node.Body != null)
                {
                    node.Body.Accept(this);
                }
            }
        }

        #endregion
    }
}
