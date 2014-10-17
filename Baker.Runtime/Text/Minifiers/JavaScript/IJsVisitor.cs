// IVisitor.cs
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
    internal interface IJsVisitor
    {
        void Visit(JsArrayLiteral node);
        void Visit(JsAspNetBlockNode node);
        void Visit(JsAstNodeList node);
        void Visit(JsBinaryOperator node);
        void Visit(JsBlock node);
        void Visit(JsBreak node);
        void Visit(JsCallNode node);
        void Visit(JsConditionalCompilationComment node);
        void Visit(JsConditionalCompilationElse node);
        void Visit(JsConditionalCompilationElseIf node);
        void Visit(JsConditionalCompilationEnd node);
        void Visit(JsConditionalCompilationIf node);
        void Visit(JsConditionalCompilationOn node);
        void Visit(JsConditionalCompilationSet node);
        void Visit(JsConditional node);
        void Visit(JsConstantWrapper node);
        void Visit(JsConstantWrapperPP node);
        void Visit(JsConstStatement node);
        void Visit(JsContinueNode node);
        void Visit(JsCustomNode node);
        void Visit(JsDebuggerNode node);
        void Visit(JsDirectivePrologue node);
        void Visit(JsDoWhile node);
        void Visit(JsEmptyStatement node);
        void Visit(JsForIn node);
        void Visit(JsForNode node);
        void Visit(JsFunctionObject node);
        void Visit(JsGetterSetter node);
        void Visit(JsGroupingOperator node);
        void Visit(JsIfNode node);
        void Visit(JsImportantComment node);
        void Visit(JsLabeledStatement node);
        void Visit(JsLexicalDeclaration node);
        void Visit(JsLookup node);
        void Visit(JsMember node);
        void Visit(JsObjectLiteral node);
        void Visit(JsObjectLiteralField node);
        void Visit(JsObjectLiteralProperty node);
        void Visit(JsParameterDeclaration node);
        void Visit(JsRegExpLiteral node);
        void Visit(JsReturnNode node);
        void Visit(JsSwitch node);
        void Visit(JsSwitchCase node);
        void Visit(JsThisLiteral node);
        void Visit(JsThrowNode node);
        void Visit(JsTryNode node);
        void Visit(JsVar node);
        void Visit(JsVariableDeclaration node);
        void Visit(JsUnaryOperator node);
        void Visit(JsWhileNode node);
        void Visit(JsWithNode node);
    }
}
