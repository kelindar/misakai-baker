// blockscope.cs
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
using System.Reflection;

namespace Baker.Text
{
    internal class JsBlockScope : JsActivationObject
    {
        private JsContext m_context;// = null;
        public JsContext Context
        {
            get { return m_context; }
        }

        public JsBlockScope(JsActivationObject parent, JsContext context, JsSettings settings)
            : base(parent, settings)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            m_context = context.Clone();
        }

        #region scope setup methods

        /// <summary>
        /// Set up this scopes lexically-declared fields
        /// </summary>
        public override void DeclareScope()
        {
            // only bind lexical declarations
            DefineLexicalDeclarations();
        }

        #endregion

        public override JsVariableField CreateField(string name, object value, FieldAttributes attributes)
        {
            return new JsVariableField(JsFieldType.Local, name, attributes, value);
        }
    }
}