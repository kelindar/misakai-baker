// jskeyword.cs
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

namespace Baker.Text
{

    internal sealed class JsKeyword
    {
        private JsKeyword m_next;
        private JsToken m_token;
        private string m_name;
        private int m_length;

        private JsKeyword(JsToken token, string name)
            : this(token, name, null)
        {
        }

        private JsKeyword(JsToken token, string name, JsKeyword next)
        {
            m_name = name;
            m_token = token;
            m_length = m_name.Length;
            m_next = next;
        }

        /*internal bool Exists(string target)
        {
            JSKeyword keyword = this;
            while (keyword != null)
            {
                if (keyword.m_name == target)
                {
                    return true;
                }
                keyword = keyword.m_next;
            }
            return false;
        }*/

        internal static string CanBeIdentifier(JsToken keyword)
        {
            switch (keyword)
            {
                // always allowed
                case JsToken.Get: return "get";
                case JsToken.Set: return "set";

                // not in strict mode
                case JsToken.Implements: return "implements";
                case JsToken.Interface: return "interface";
                case JsToken.Let: return "let";
                case JsToken.Package: return "package";
                case JsToken.Private: return "private";
                case JsToken.Protected: return "protected";
                case JsToken.Public: return "public";
                case JsToken.Static: return "static";
                case JsToken.Yield: return "yield";

                // apparently never allowed for Chrome, so we want to treat it
                // differently, too
                case JsToken.Native: return "native";

                // no other tokens can be identifiers
                default: return null;
            }
        }

        internal JsToken GetKeyword(JsContext context, int wordLength)
        {
            return GetKeyword(context.Document.Source, context.StartPosition, wordLength);
        }

        internal JsToken GetKeyword(string source, int startPosition, int wordLength)
        {
            JsKeyword keyword = this;

        nextToken:
            while (null != keyword)
            {
                if (wordLength == keyword.m_length)
                {
                    // equal number of characters
                    // we know the first char has to match, so start with the second
                    for (int i = 1, j = startPosition + 1; i < wordLength; i++, j++)
                    {
                        char ch1 = keyword.m_name[i];
                        char ch2 = source[j];
                        if (ch1 == ch2)
                        {
                            // match -- continue
                            continue;
                        }
                        else if (ch2 < ch1)
                        {
                            // because the list is in order, if the character for the test
                            // is less than the character for the keyword we are testing against,
                            // then we know this isn't going to be in any other node
                            return JsToken.Identifier;
                        }
                        else
                        {
                            // must be greater than the current token -- try the next one
                            keyword = keyword.m_next;
                            goto nextToken;
                        }
                    }

                    // if we got this far, it was a complete match
                    return keyword.m_token;
                }
                else if (wordLength < keyword.m_length)
                {
                    // in word-length order first of all, so if the length of the test string is
                    // less than the length of the keyword node, this is an identifier
                    return JsToken.Identifier;
                }

                keyword = keyword.m_next;
            }
            return JsToken.Identifier;
        }

        // each list must in order or length first, shortest to longest.
        // for equal length words, in alphabetical order
        internal static JsKeyword[] InitKeywords()
        {
            JsKeyword[] keywords = new JsKeyword[26];
            // a
            // b
            keywords['b' - 'a'] = new JsKeyword(JsToken.Break, "break");
            // c
            keywords['c' - 'a'] = new JsKeyword(JsToken.Case, "case",
                new JsKeyword(JsToken.Catch, "catch",
                    new JsKeyword(JsToken.Class, "class",
                        new JsKeyword(JsToken.Const, "const", 
                            new JsKeyword(JsToken.Continue, "continue")))));
            // d
            keywords['d' - 'a'] = new JsKeyword(JsToken.Do, "do", 
                new JsKeyword(JsToken.Delete, "delete",
                    new JsKeyword(JsToken.Default, "default", 
                        new JsKeyword(JsToken.Debugger, "debugger"))));
            // e
            keywords['e' - 'a'] = new JsKeyword(JsToken.Else, "else",
                new JsKeyword(JsToken.Enum, "enum", 
                    new JsKeyword(JsToken.Export, "export", 
                        new JsKeyword(JsToken.Extends, "extends"))));
            // f
            keywords['f' - 'a'] = new JsKeyword(JsToken.For, "for", 
                new JsKeyword(JsToken.False, "false", 
                    new JsKeyword(JsToken.Finally, "finally",
                        new JsKeyword(JsToken.Function, "function"))));
            // g
            keywords['g' - 'a'] = new JsKeyword(JsToken.Get, "get");
            // i
            keywords['i' - 'a'] = new JsKeyword(JsToken.If, "if",
                new JsKeyword(JsToken.In, "in", 
                    new JsKeyword(JsToken.Import, "import", 
                        new JsKeyword(JsToken.Interface, "interface",
                            new JsKeyword(JsToken.Implements, "implements",
                                new JsKeyword(JsToken.InstanceOf, "instanceof"))))));
            // l
            keywords['l' - 'a'] = new JsKeyword(JsToken.Let, "let");
            // n
            keywords['n' - 'a'] = new JsKeyword(JsToken.New, "new",
                new JsKeyword(JsToken.Null, "null",
                    new JsKeyword(JsToken.Native, "native")));
            // p
            keywords['p' - 'a'] = new JsKeyword(JsToken.Public, "public",
                new JsKeyword(JsToken.Package, "package",
                    new JsKeyword(JsToken.Private, "private", 
                        new JsKeyword(JsToken.Protected, "protected"))));
            // r
            keywords['r' - 'a'] = new JsKeyword(JsToken.Return, "return");
            // s
            keywords['s' - 'a'] = new JsKeyword(JsToken.Set, "set",
                new JsKeyword(JsToken.Super, "super", 
                    new JsKeyword(JsToken.Static, "static",
                        new JsKeyword(JsToken.Switch, "switch"))));
            // t
            keywords['t' - 'a'] = new JsKeyword(JsToken.Try, "try", 
                new JsKeyword(JsToken.This, "this",
                    new JsKeyword(JsToken.True, "true", 
                        new JsKeyword(JsToken.Throw, "throw",
                            new JsKeyword(JsToken.TypeOf, "typeof")))));
            // u
            // v
            keywords['v' - 'a'] = new JsKeyword(JsToken.Var, "var", 
                new JsKeyword(JsToken.Void, "void"));
            // w
            keywords['w' - 'a'] = new JsKeyword(JsToken.With, "with",
                new JsKeyword(JsToken.While, "while"));
            // y
            keywords['y' - 'a'] = new JsKeyword(JsToken.Yield, "yield");

            return keywords;
        }
    }
}
