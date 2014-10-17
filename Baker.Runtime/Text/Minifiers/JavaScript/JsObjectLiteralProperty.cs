// ObjectLiteralProperty.cs
//
// Copyright 2012 Microsoft Corporation
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
using System.Text;

namespace Baker.Text
{
    internal class JsObjectLiteralProperty : JsAstNode
    {
        private JsObjectLiteralField m_propertyName;
        private JsAstNode m_propertyValue;

        public JsObjectLiteralField Name
        {
            get { return m_propertyName; }
            set
            {
                m_propertyName.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_propertyName = value;
                m_propertyName.IfNotNull(n => n.Parent = this);
            }
        }

        public JsAstNode Value
        {
            get { return m_propertyValue; }
            set
            {
                m_propertyValue.IfNotNull(n => n.Parent = (n.Parent == this) ? null : n.Parent);
                m_propertyValue = value;
                m_propertyValue.IfNotNull(n => n.Parent = this);
            }
        }

        public override bool IsConstant
        {
            get
            {
                // we are constant if our value is constant.
                // If we don't have a value, then assume it's constant?
                return Value != null ? Value.IsConstant : true;
            }
        }

        public JsObjectLiteralProperty(JsContext context, JsParser parser)
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

        public override IEnumerable<JsAstNode> Children
        {
            get
            {
                return EnumerateNonNullNodes(Name, Value);
            }
        }

        public override bool ReplaceChild(JsAstNode oldNode, JsAstNode newNode)
        {
            if (Name == oldNode)
            {
                var objectField = newNode as JsObjectLiteralField;
                if (newNode == null || objectField != null)
                {
                    Name = objectField;
                }
                return true;
            }

            if (Value == oldNode)
            {
                Value = newNode;
                return true;
            }

            return false;
        }

        internal override string GetFunctionGuess(JsAstNode target)
        {
            return Name.ToString();
        }
    }
}
