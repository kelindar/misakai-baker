// jsparser.cs
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Baker.Text
{
    /// <summary>
    /// Class used to parse JavaScript source code into an abstract syntax tree.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
    internal class JsParser
    {
        private const int c_MaxSkippedTokenNumber = 50;

        private JsDocumentContext m_document;
        private JsScanner m_scanner;
        private JsContext m_currentToken;

        // used for errors to flag that the same token has to be returned.
        // We could have used just a boolean but having a Context does not
        // add any overhead and allow to really save the info, if that will ever be needed
        private bool m_useCurrentForNext;
        private int m_tokensSkipped;
        private NoSkipTokenSet m_noSkipTokenSet;
        private long m_goodTokensProcessed;

        private bool m_newModule;

        // we're going to copy the debug lookups from the settings passed to us,
        // then use this collection, because we might programmatically add more
        // as we process the code, and we don't want to change the settings object.
        public ICollection<string> DebugLookups { get; private set; }

        // label related info
        private List<BlockType> m_blockType;
        private Dictionary<string, LabelInfo> m_labelTable;
        enum BlockType { Block, Loop, Switch, Finally }
        private int m_finallyEscaped;

        private bool m_foundEndOfLine;
        private IList<JsContext> m_importantComments;

        private class LabelInfo
        {
            public readonly int BlockIndex;
            public readonly int NestLevel;

            public LabelInfo(int blockIndex, int nestLevel)
            {
                BlockIndex = blockIndex;
                NestLevel = nestLevel;
            }
        }

        public JsSettings Settings
        {
            get
            {
                // if it's null....
                if (m_settings == null)
                {
                    // just use the default settings
                    m_settings = new JsSettings();
                }
                return m_settings;
            }
        }
        private JsSettings m_settings;// = null;

        private int m_breakRecursion;// = 0;
        private int m_severity;

        /// <summary>
        /// Gets or sets a TextWriter instance to which raw preprocessed input will be
        /// written when Parse is called.
        /// </summary>
        public TextWriter EchoWriter { get; set; }

        private long[] m_timingPoints;
        public IList<long> TimingPoints { get { return m_timingPoints; } }

        public event EventHandler<JScriptExceptionEventArgs> CompilerError;
        public event EventHandler<UndefinedReferenceEventArgs> UndefinedReference;

        public JsGlobalScope GlobalScope
        {
            get
            {
                // if we don't have one yet, create a new one
                if (m_globalScope == null)
                {
                    m_globalScope = new JsGlobalScope(m_settings);
                }

                return m_globalScope;
            }
            set
            {
                // if we are setting the global scope, we are using a shared global scope.
                m_globalScope = value;

                // mark all existing child scopes as existing so we don't go through
                // them again and re-optimize
                if (m_globalScope != null)
                {
                    foreach (var childScope in m_globalScope.ChildScopes)
                    {
                        childScope.Existing = true;
                    }
                }
            }
        }
        private JsGlobalScope m_globalScope;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Microsoft.Ajax.Utilities.ContextError.#ctor(System.Boolean,System.Int32,System.String,System.String,System.String,System.String,System.Int32,System.Int32,System.Int32,System.Int32,System.String)")]
        internal bool OnCompilerError(JsException se)
        {
            if (CompilerError != null && !m_settings.IgnoreAllErrors)
            {
                // format the error code
                string errorCode = "JS{0}".FormatInvariant((int)se.ErrorCode);
                if (m_settings != null && !m_settings.IgnoreErrorCollection.Contains(errorCode))
                {
                    // get the offending context
                    string context = se.ErrorSegment;

                    // if the context is empty, use the whole line
                    if (!context.IsNullOrWhiteSpace())
                    {
                        context = ": " + context;
                    }

                    CompilerError(this, new JScriptExceptionEventArgs(se, new MinifierError(
                        se.IsError,
                        se.Severity,
                        GetSeverityString(se.Severity),
                        errorCode,
                        null,
                        se.FileContext,
                        se.Line,
                        se.Column,
                        se.EndLine,
                        se.EndColumn,
                        se.Message + context)));
                }
            }

            //true means carry on with compilation.
            return se.CanRecover;
        }

        private static string GetSeverityString(int severity)
        {
            // From jscriptexception.js:
            //
            //guide: 0 == there will be a run-time error if this code executes
            //       1 == the programmer probably did not intend to do this
            //       2 == this can lead to problems in the future.
            //       3 == this can lead to performance problems
            //       4 == this is just not right
            switch (severity)
            {
                case 0:
                    return JScript.Severity0;

                case 1:
                    return JScript.Severity1;

                case 2:
                    return JScript.Severity2;

                case 3:
                    return JScript.Severity3;

                case 4:
                    return JScript.Severity4;

                default:
                    return JScript.SeverityUnknown.FormatInvariant(severity);
            }
        }

        internal void OnUndefinedReference(UndefinedReferenceException ex)
        {
            if (UndefinedReference != null)
            {
                UndefinedReference(this, new UndefinedReferenceEventArgs(ex));
            }
        }

        /// <summary>
        /// Creates an instance of the JSParser class that can be used to parse the given source code.
        /// </summary>
        /// <param name="source">Source code to parse.</param>
        public JsParser(string source)
        {
            m_severity = 5;
            m_blockType = new List<BlockType>(16);
            m_labelTable = new Dictionary<string, LabelInfo>();
            m_noSkipTokenSet = new NoSkipTokenSet();
            m_importantComments = new List<JsContext>();

            m_document = new JsDocumentContext(this, source);
            m_scanner = new JsScanner(new JsContext(m_document));
            m_currentToken = new JsContext(m_document);

            // if the scanner encounters a special "globals" comment, it'll fire this event
            // at which point we will define a field with that name in the global scope. 
            m_scanner.GlobalDefine += (sender, ea) =>
                {
                    var globalScope = GlobalScope;
                    if (globalScope[ea.Name] == null)
                    {
                        var field = globalScope.CreateField(ea.Name, null, FieldAttributes.SpecialName);
                        globalScope.AddField(field);
                    }
                };

            // this event is fired whenever a ///#SOURCE comment is encountered
            m_scanner.NewModule += (sender, ea) =>
                {
                    m_newModule = true;

                    // we also want to assume that we found a newline character after
                    // the comment
                    m_foundEndOfLine = true;
                };
        }

        /// <summary>
        /// Gets or sets the file context for the given source code. This context will be used when generating any error messages.
        /// </summary>
        public string FileContext
        {
            get 
            { 
                return m_document.FileContext; 
            }
            set 
            { 
                m_document.FileContext = value; 
            }
        }

        private void InitializeScanner(JsSettings settings)
        {
            // save the settings
            // if we are passed null, just create a default settings object
            m_settings = settings = settings ?? new JsSettings();

            // if the settings list is not null, use it to initialize a new list
            // with the same settings. If it is null, initialize an empty list 
            // because we already determined that we want to strip debug statements,
            // and the scanner might add items to the list as it scans the source.
            DebugLookups = new HashSet<string>(m_settings.DebugLookupCollection);

            // pass our list to the scanner -- it might add more as we encounter special comments
            m_scanner.DebugLookupCollection = DebugLookups;

            m_scanner.AllowEmbeddedAspNetBlocks = m_settings.AllowEmbeddedAspNetBlocks;
            m_scanner.IgnoreConditionalCompilation = m_settings.IgnoreConditionalCompilation;

            // set any defines
            m_scanner.UsePreprocessorDefines = !m_settings.IgnorePreprocessorDefines;
            if (m_scanner.UsePreprocessorDefines)
            {
                m_scanner.SetPreprocessorDefines(m_settings.PreprocessorValues);
            }

            // if we want to strip debug statements, let's also strip ///#DEBUG comment
            // blocks for legacy reasons. ///#DEBUG will get stripped ONLY is this
            // flag is true AND the name "DEBUG" is not in the preprocessor defines.
            // Alternately, we will keep those blocks in the output is this flag is
            // set to false OR we define "DEBUG" in the preprocessor defines.
            m_scanner.StripDebugCommentBlocks = m_settings.StripDebugStatements;
        }

        #region pre-process only

        /// <summary>
        /// Obsolete - set the PreprocessOnly property on the CodeSettings class to true and call Parse method.
        /// Preprocess the input only - don't generate an AST tree or do any other code analysis, just return the processed code as a string. 
        /// </summary>
        /// <param name="settings">settings to use in the scanner</param>
        /// <returns>the source as processed by the preprocessor</returns>
        [Obsolete("Set EchoWriter property to and call Parse method with PreprocessOnly property on the CodeSettings object set to true", true)]
        public string PreprocessOnly(JsSettings settings)
        {
            // create an empty string builder
            using (var outputStream = new StringWriter(CultureInfo.InvariantCulture))
            {
                // output to the string builder
                PreprocessOnly(settings, outputStream);

                // return the resulting text
                return outputStream.ToString();
            }
        }

        /// <summary>
        /// Preprocess the input only - don't generate a syntax tree or do any other code analysis. Just write the processed
        /// code to the provided text stream.
        /// </summary>
        /// <param name="settings">settings to use in the scanner</param>
        /// <param name="outputStream">output stream to which to write the processed source</param>
        [Obsolete("Set EchoWriter property to and call Parse method with PreprocessOnly property on the CodeSettings object set to true", true)]
        public void PreprocessOnly(JsSettings settings, TextWriter outputStream)
        {
            if (outputStream != null)
            {
                EchoWriter = outputStream;
                Parse(settings);
                EchoWriter = null;

                if (m_settings.TermSemicolons)
                {
                    // if we want to make sure this file has a terminating semicolon, start a new line
                    // (to make sure any single-line comments are terminated) and output a semicolon
                    // followed by another line break.
                    outputStream.WriteLine();
                    outputStream.WriteLine(';');
                }
            }
        }

        #endregion

        /// <summary>
        /// Parse the source code using the given settings, getting back an abstract syntax tree Block node as the root
        /// representing the list of statements in the source code.
        /// </summary>
        /// <param name="settings">code settings to use to process the source code</param>
        /// <returns>root Block node representing the top-level statements</returns>
        public JsBlock Parse(JsSettings settings)
        {
            // initialize the scanner with our settings
            // make sure the RawTokens setting is OFF or we won't be able to create our AST
            InitializeScanner(settings);

            // make sure we initialize the global scope's strict mode to our flag, whether or not it
            // is true. This means if the setting is false, we will RESET the flag to false if we are 
            // reusing the scope and a previous Parse call had code that set it to strict with a 
            // program directive. 
            GlobalScope.UseStrict = m_settings.StrictMode;

            // make sure the global scope knows about our known global names
            GlobalScope.SetAssumedGlobals(m_settings);

            // start of a new module
            m_newModule = true;

            var timePoints = m_timingPoints = new long[9];
            var timeIndex = timePoints.Length;
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            JsBlock scriptBlock = null;
            JsBlock returnBlock = null;
            try
            {
                switch (m_settings.SourceMode)
                {
                    case JsSourceMode.Program:
                        // simply parse a block of statements
                        returnBlock = scriptBlock = ParseStatements();
                        break;

                    case JsSourceMode.Expression:
                        // create a block, get the first token, add in the parse of a single expression, 
                        // and we'll go fron there.
                        returnBlock = scriptBlock = new JsBlock(CurrentPositionContext(), this);
                        GetNextToken();
                        try
                        {
                            var expr = ParseExpression();
                            if (expr != null)
                            {
                                scriptBlock.Append(expr);
                                scriptBlock.UpdateWith(expr.Context);
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            Debug.WriteLine("EOF");
                        }
                        break;

                    case JsSourceMode.EventHandler:
                        // we're going to create the global block, add in a function expression with a single
                        // parameter named "event", and then we're going to parse the input as the body of that
                        // function expression. We're going to resolve the global block, but only return the body
                        // of the function.
                        scriptBlock = new JsBlock(null, this);

                        var parameters = new JsAstNodeList(null, this);
                        parameters.Append(new JsParameterDeclaration(null, this)
                            {
                                Name = "event",
                                RenameNotAllowed = true
                            });

                        var funcExpression = new JsFunctionObject(null, this)
                            {
                                FunctionType = JsFunctionType.Expression,
                                ParameterDeclarations = parameters
                            };
                        scriptBlock.Append(funcExpression);
                        funcExpression.Body = returnBlock = ParseStatements();
                        break;

                    default:
                        Debug.Fail("Unexpected source mode enumeration");
                        return null;
                }
            }
            catch (RecoveryTokenException)
            {
                // this should never happen but let's make SURE we don't expose our
                // private exception object to the outside world
                m_currentToken.HandleError(JsError.ApplicationError, true);
            }

            timePoints[--timeIndex] = stopWatch.ElapsedTicks;

            if (scriptBlock != null)
            {
                // resolve everything
                JsResolutionVisitor.Apply(scriptBlock, GlobalScope, m_settings);
            }

            timePoints[--timeIndex] = stopWatch.ElapsedTicks;

            if (scriptBlock != null && Settings.MinifyCode && !Settings.PreprocessOnly)
            {
                // this visitor doesn't just reorder scopes. It also combines the adjacent var variables,
                // unnests blocks, identifies prologue directives, and sets the strict mode on scopes. 
                JsReorderScopeVisitor.Apply(scriptBlock, this);
                timePoints[--timeIndex] = stopWatch.ElapsedTicks;

                // analyze the entire node tree (needed for hypercrunch)
                // root to leaf (top down)
                var analyzeVisitor = new JsAnalyzeNodeVisitor(this);
                scriptBlock.Accept(analyzeVisitor);
                timePoints[--timeIndex] = stopWatch.ElapsedTicks;

                // analyze the scope chain (also needed for hypercrunch)
                // root to leaf (top down)
                GlobalScope.AnalyzeScope();
                timePoints[--timeIndex] = stopWatch.ElapsedTicks;

                // if we want to crunch any names....
                if (m_settings.LocalRenaming != JsLocalRenaming.KeepAll
                    && m_settings.IsModificationAllowed(JsTreeModifications.LocalRenaming))
                {
                    // then do a top-down traversal of the scope tree. For each field that had not
                    // already been crunched (globals and outers will already be crunched), crunch
                    // the name with a crunch iterator that does not use any names in the verboten set.
                    GlobalScope.AutoRenameFields();
                }

                timePoints[--timeIndex] = stopWatch.ElapsedTicks;

                // if we want to evaluate literal expressions, do so now
                if (m_settings.EvalLiteralExpressions)
                {
                    var visitor = new JsEvaluateLiteralVisitor(this);
                    scriptBlock.Accept(visitor);
                }

                timePoints[--timeIndex] = stopWatch.ElapsedTicks;

                // make the final cleanup pass
                JsFinalPassVisitor.Apply(scriptBlock, this);
                timePoints[--timeIndex] = stopWatch.ElapsedTicks;

                // we want to walk all the scopes to make sure that any generated
                // variables that haven't been crunched have been assigned valid
                // variable names that don't collide with any existing variables.
                GlobalScope.ValidateGeneratedNames();
                timePoints[--timeIndex] = stopWatch.ElapsedTicks;
            }

            if (returnBlock != null && returnBlock.Parent != null)
            {
                returnBlock.Parent = null;
            }

            return returnBlock;
        }

        /// <summary>
        /// Parse an expression from the source code and return a block node containing just that expression.
        /// The block node is needed because we might perform optimization on the expression that creates
        /// a new expression, and we need a parent to contain it.
        /// </summary>
        /// <param name="settings">settings to use</param>
        /// <returns>a block node containing the parsed expression as its only child</returns>
        [Obsolete("This property is deprecated; call Parse with CodeSettings.SourceMode set to JavaScriptSourceMode.Expression instead")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public JsBlock ParseExpression(JsSettings settings)
        {
            // we need to make sure the settings object has the expression source mode property set,
            // but let's not modify the settings object passed in. So clone it, set the property on the
            // clone, and use that object for parsing.
            settings = settings == null ? new JsSettings() : settings.Clone();
            settings.SourceMode = JsSourceMode.Expression;
            return Parse(settings);
        }

        //---------------------------------------------------------------------------------------
        // ParseStatements
        //
        // statements :
        //   <empty> |
        //   statement statements
        //
        //---------------------------------------------------------------------------------------
        private JsBlock ParseStatements()
        {
            var program = new JsBlock(CurrentPositionContext(), this);
            m_blockType.Add(BlockType.Block);
            m_useCurrentForNext = false;
            try
            {
                // get the first token
                GetNextToken();
                
                // if the block doesn't have a proper file context, then let's set it from the 
                // first token -- that token might have had a ///#source directive!
                if (string.IsNullOrEmpty(program.Context.Document.FileContext))
                {
                    program.Context.Document.FileContext = m_currentToken.Document.FileContext;
                }

                m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_TopLevelNoSkipTokenSet);

                try
                {
                    var possibleDirectivePrologue = true;
                    int lastEndPosition = m_currentToken.EndPosition;
                    while (m_currentToken.Token != JsToken.EndOfFile)
                    {
                        JsAstNode ast = null;
                        try
                        {
                            // parse a statement -- pass true because we really want a SourceElement,
                            // which is a Statement OR a FunctionDeclaration. Technically, FunctionDeclarations
                            // are not statements!
                            ast = ParseStatement(true);

                            // if we are still possibly looking for directive prologues
                            if (possibleDirectivePrologue)
                            {
                                var constantWrapper = ast as JsConstantWrapper;
                                if (constantWrapper != null && constantWrapper.PrimitiveType == JsPrimitiveType.String)
                                {
                                    if (!(constantWrapper is JsDirectivePrologue))
                                    {
                                        // use a directive prologue node instead
                                        ast = new JsDirectivePrologue(constantWrapper.Value.ToString(), ast.Context, ast.Parser)
                                            {
                                                MayHaveIssues = constantWrapper.MayHaveIssues
                                            };
                                    }
                                }
                                else if (!m_newModule)
                                {
                                    // nope -- no longer finding directive prologues
                                    possibleDirectivePrologue = false;
                                }
                            }
                            else if (m_newModule)
                            {
                                // we aren't looking for directive prologues anymore, but we did scan
                                // into a new module after that last AST, so reset the flag because that
                                // new module might have directive prologues for next time
                                possibleDirectivePrologue = true;
                            }
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (TokenInList(NoSkipTokenSet.s_TopLevelNoSkipTokenSet, exc)
                                || TokenInList(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc))
                            {
                                ast = exc._partiallyComputedNode;
                                GetNextToken();
                            }
                            else
                            {
                                m_useCurrentForNext = false;
                                do
                                {
                                    GetNextToken();
                                } while (m_currentToken.Token != JsToken.EndOfFile && !TokenInList(NoSkipTokenSet.s_TopLevelNoSkipTokenSet, m_currentToken.Token)
                                  && !TokenInList(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, m_currentToken.Token));
                            }
                        }

                        if (null != ast)
                        {
                            // append the token to the program
                            program.Append(ast);

                            // set the last end position to be the start of the current token.
                            // if we parse the next statement and the end is still the start, we know
                            // something is up and might get into an infinite loop.
                            lastEndPosition = m_currentToken.EndPosition;
                        }
                        else if (!m_scanner.IsEndOfFile && m_currentToken.StartLinePosition == lastEndPosition)
                        {
                            // didn't parse a statement, we're not at the EOF, and we didn't move
                            // anywhere in the input stream. If we just keep looping around, we're going
                            // to get into an infinite loop. Break it.
                            m_currentToken.HandleError(JsError.ApplicationError, true);
                            break;
                        }
                    }

                    AppendImportantComments(program);
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_TopLevelNoSkipTokenSet);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                }

            }
            catch (EndOfStreamException)
            {
            }

            program.UpdateWith(CurrentPositionContext());
            return program;
        }

        //---------------------------------------------------------------------------------------
        // ParseStatement
        //
        //  OptionalStatement:
        //    Statement |
        //    <empty>
        //
        //  Statement :
        //    Block |
        //  VariableStatement |
        //  EmptyStatement |
        //  ExpressionStatement |
        //  IfStatement |
        //  IterationStatement |
        //  ContinueStatement |
        //  BreakStatement |
        //  ReturnStatement |
        //  WithStatement |
        //  LabeledStatement |
        //  SwitchStatement |
        //  ThrowStatement |
        //  TryStatement |
        //  FunctionDeclaration
        //
        // IterationStatement :
        //    'for' '(' ForLoopControl ')' |                  ===> ForStatement
        //    'do' Statement 'while' '(' Expression ')' |     ===> DoStatement
        //    'while' '(' Expression ')' Statement            ===> WhileStatement
        //
        //---------------------------------------------------------------------------------------

        // ParseStatement deals with the end of statement issue (EOL vs ';') so if any of the
        // ParseXXX routine does it as well, it should return directly from the switch statement
        // without any further execution in the ParseStatement
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode")]
        private JsAstNode ParseStatement(bool fSourceElement, bool skipImportantComment = false)
        {
            JsAstNode statement = null;

            // if we want to skip important comments, now is a good time to clear anything we may 
            // have picked up already.
            if (skipImportantComment)
            {
                m_importantComments.Clear();
            }

            if (m_importantComments.Count > 0 
                && m_settings.PreserveImportantComments
                && m_settings.IsModificationAllowed(JsTreeModifications.PreserveImportantComments))
            {
                // we have at least one important comment before the upcoming statement.
                // pop the first important comment off the queue, return that node instead.
                // don't advance the token -- we'll probably be coming back again for the next one (if any)
                statement = new JsImportantComment(m_importantComments[0], this);
                m_importantComments.RemoveAt(0);
            }
            else
            {
                String id = null;
                var isNewModule = m_newModule;

                switch (m_currentToken.Token)
                {
                    case JsToken.EndOfFile:
                        EOFError(JsError.ErrorEndOfFile);
                        throw new EndOfStreamException(); // abort parsing, get back to the main parse routine
                    case JsToken.Semicolon:
                        // make an empty statement
                        statement = new JsEmptyStatement(m_currentToken.Clone(), this);
                        GetNextToken();
                        return statement;
                    case JsToken.RightCurly:
                        ReportError(JsError.SyntaxError);
                        SkipTokensAndThrow();
                        break;
                    case JsToken.LeftCurly:
                        return ParseBlock();
                    case JsToken.Debugger:
                        return ParseDebuggerStatement();
                    case JsToken.Var:
                    case JsToken.Const:
                    case JsToken.Let:
                        return ParseVariableStatement();
                    case JsToken.If:
                        return ParseIfStatement();
                    case JsToken.For:
                        return ParseForStatement();
                    case JsToken.Do:
                        return ParseDoStatement();
                    case JsToken.While:
                        return ParseWhileStatement();
                    case JsToken.Continue:
                        statement = ParseContinueStatement();
                        if (null == statement)
                            return new JsBlock(CurrentPositionContext(), this);
                        else
                            return statement;
                    case JsToken.Break:
                        statement = ParseBreakStatement();
                        if (null == statement)
                            return new JsBlock(CurrentPositionContext(), this);
                        else
                            return statement;
                    case JsToken.Return:
                        statement = ParseReturnStatement();
                        if (null == statement)
                            return new JsBlock(CurrentPositionContext(), this);
                        else
                            return statement;
                    case JsToken.With:
                        return ParseWithStatement();
                    case JsToken.Switch:
                        return ParseSwitchStatement();
                    case JsToken.Throw:
                        statement = ParseThrowStatement();
                        if (statement == null)
                            return new JsBlock(CurrentPositionContext(), this);
                        else
                            break;
                    case JsToken.Try:
                        return ParseTryStatement();
                    case JsToken.Function:
                        // parse a function declaration
                        JsFunctionObject function = ParseFunction(JsFunctionType.Declaration, m_currentToken.Clone());
                        function.IsSourceElement = fSourceElement;
                        return function;
                    case JsToken.Else:
                        ReportError(JsError.InvalidElse);
                        SkipTokensAndThrow();
                        break;
                    case JsToken.ConditionalCommentStart:
                        return ParseStatementLevelConditionalComment(fSourceElement);
                    case JsToken.ConditionalCompilationOn:
                        {
                            JsConditionalCompilationOn ccOn = new JsConditionalCompilationOn(m_currentToken.Clone(), this);
                            GetNextToken();
                            return ccOn;
                        }
                    case JsToken.ConditionalCompilationSet:
                        return ParseConditionalCompilationSet();
                    case JsToken.ConditionalCompilationIf:
                        return ParseConditionalCompilationIf(false);
                    case JsToken.ConditionalCompilationElseIf:
                        return ParseConditionalCompilationIf(true);
                    case JsToken.ConditionalCompilationElse:
                        {
                            JsConditionalCompilationElse elseStatement = new JsConditionalCompilationElse(m_currentToken.Clone(), this);
                            GetNextToken();
                            return elseStatement;
                        }
                    case JsToken.ConditionalCompilationEnd:
                        {
                            JsConditionalCompilationEnd endStatement = new JsConditionalCompilationEnd(m_currentToken.Clone(), this);
                            GetNextToken();
                            return endStatement;
                        }

                    default:
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                        bool exprError = false;
                        try
                        {
                            bool bAssign;
                            statement = ParseUnaryExpression(out bAssign, false);

                            // look for labels
                            if (statement is JsLookup && JsToken.Colon == m_currentToken.Token)
                            {
                                // can be a label
                                id = statement.ToString();
                                if (m_labelTable.ContainsKey(id))
                                {
                                    // there is already a label with that name. Ignore the current label
                                    ReportError(JsError.BadLabel, statement.Context.Clone(), true);
                                    id = null;
                                    GetNextToken(); // skip over ':'
                                    return new JsBlock(CurrentPositionContext(), this);
                                }
                                else
                                {
                                    var colonContext = m_currentToken.Clone();
                                    GetNextToken();
                                    int labelNestCount = m_labelTable.Count + 1;
                                    m_labelTable.Add(id, new LabelInfo(m_blockType.Count, labelNestCount));
                                    if (JsToken.EndOfFile != m_currentToken.Token)
                                    {
                                        // ignore any important comments between the label and its statement
                                        // because important comments are treated like statements, and we want
                                        // to make sure the label is attached to the right REAL statement.
                                        statement = new JsLabeledStatement(statement.Context.Clone(), this)
                                            {
                                                Label = id,
                                                ColonContext = colonContext,
                                                NestCount = labelNestCount,
                                                Statement = ParseStatement(fSourceElement, true)
                                            };
                                    }
                                    else
                                    {
                                        // end of the file!
                                        //just pass null for the labeled statement
                                        statement = new JsLabeledStatement(statement.Context.Clone(), this)
                                            {
                                                Label = id,
                                                ColonContext = colonContext,
                                                NestCount = labelNestCount
                                            };
                                    }
                                    m_labelTable.Remove(id);
                                    return statement;
                                }
                            }
                            statement = ParseExpression(statement, false, bAssign, JsToken.None);

                            // if we just started a new module and this statement happens to be an expression statement...
                            if (isNewModule && statement.IsExpression)
                            {
                                // see if it's a constant wrapper
                                var constantWrapper = statement as JsConstantWrapper;
                                if (constantWrapper != null && constantWrapper.PrimitiveType == JsPrimitiveType.String)
                                {
                                    // we found a string constant expression statement right after the start of a new
                                    // module. Let's make it a DirectivePrologue if it isn't already
                                    if (!(statement is JsDirectivePrologue))
                                    {
                                        statement = new JsDirectivePrologue(constantWrapper.Value.ToString(), constantWrapper.Context, this)
                                            {
                                                MayHaveIssues = constantWrapper.MayHaveIssues
                                            };
                                    }
                                }
                            }

                            var binaryOp = statement as JsBinaryOperator;
                            if (binaryOp != null
                                && (binaryOp.OperatorToken == JsToken.Equal || binaryOp.OperatorToken == JsToken.StrictEqual))
                            {
                                // an expression statement with equality operator? Doesn't really do anything.
                                // Did the developer intend this to be an assignment operator instead? Low-pri warning.
                                binaryOp.OperatorContext.IfNotNull(c => c.HandleError(JsError.SuspectEquality, false));
                            }

                            var lookup = statement as JsLookup;
                            if (lookup != null
                                && lookup.Name.StartsWith("<%=", StringComparison.Ordinal) && lookup.Name.EndsWith("%>", StringComparison.Ordinal))
                            {
                                // single lookup, but it's actually one or more ASP.NET blocks.
                                // convert back to an asp.net block node
                                statement = new JsAspNetBlockNode(statement.Context, this)
                                {
                                    AspNetBlockText = lookup.Name
                                };
                            }

                            var aspNetBlock = statement as JsAspNetBlockNode;
                            if (aspNetBlock != null && JsToken.Semicolon == m_currentToken.Token)
                            {
                                aspNetBlock.IsTerminatedByExplicitSemicolon = true;
                                statement.IfNotNull(s => s.TerminatingContext = m_currentToken.Clone());
                                GetNextToken();
                            }

                            // we just parsed an expression statement. Now see if we have an appropriate
                            // semicolon to terminate it.
                            if (JsToken.Semicolon == m_currentToken.Token)
                            {
                                statement.IfNotNull(s => s.TerminatingContext = m_currentToken.Clone());
                                GetNextToken();
                            }
                            else if (m_foundEndOfLine || JsToken.RightCurly == m_currentToken.Token || JsToken.EndOfFile == m_currentToken.Token)
                            {
                                // semicolon insertion rules
                                // (if there was no statement parsed, then don't fire a warning)
                                // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                                // Just too common and doesn't really warrant a warning (in my opinion)
                                if (statement != null
                                    && JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                                {
                                    ReportError(JsError.SemicolonInsertion, statement.Context.IfNotNull(c => c.FlattenToEnd()), true);
                                }
                            }
                            else
                            {
                                ReportError(JsError.NoSemicolon, true);
                            }
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (exc._partiallyComputedNode != null)
                                statement = exc._partiallyComputedNode;

                            if (statement == null)
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                                exprError = true;
                                SkipTokensAndThrow();
                            }

                            if (IndexOfToken(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet, exc) == -1)
                            {
                                exc._partiallyComputedNode = statement;
                                throw;
                            }
                        }
                        finally
                        {
                            if (!exprError)
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                        }
                        break;
                }
            }

            return statement;
        }

        JsAstNode ParseStatementLevelConditionalComment(bool fSourceElement)
        {
            JsContext context = m_currentToken.Clone();
            JsConditionalCompilationComment conditionalComment = new JsConditionalCompilationComment(context, this);

            GetNextToken();
            while(m_currentToken.Token != JsToken.ConditionalCommentEnd && m_currentToken.Token != JsToken.EndOfFile)
            {
                // if we get ANOTHER start token, it's superfluous and we should ignore it.
                // otherwise parse another statement and keep going
                if (m_currentToken.Token == JsToken.ConditionalCommentStart)
                {
                    GetNextToken();
                }
                else
                {
                    conditionalComment.Append(ParseStatement(fSourceElement));
                }
            }

            GetNextToken();

            // if the conditional comment is empty (for whatever reason), then
            // we don't want to return anything -- we found nothing.
            return conditionalComment.Statements.Count > 0 ? conditionalComment : null;
        }

        JsConditionalCompilationSet ParseConditionalCompilationSet()
        {
            JsContext context = m_currentToken.Clone();
            string variableName = null;
            JsAstNode value = null;
            GetNextToken();
            if (m_currentToken.Token == JsToken.ConditionalCompilationVariable)
            {
                context.UpdateWith(m_currentToken);
                variableName = m_currentToken.Code;
                GetNextToken();
                if (m_currentToken.Token == JsToken.Assign)
                {
                    context.UpdateWith(m_currentToken);
                    GetNextToken();
                    value = ParseExpression(false);
                    if (value != null)
                    {
                        context.UpdateWith(value.Context);
                    }
                    else
                    {
                        m_currentToken.HandleError(JsError.ExpressionExpected);
                    }
                }
                else
                {
                    m_currentToken.HandleError(JsError.NoEqual);
                }
            }
            else
            {
                m_currentToken.HandleError(JsError.NoIdentifier);
            }

            return new JsConditionalCompilationSet(context, this)
                {
                    VariableName = variableName,
                    Value = value
                };
        }

        JsConditionalCompilationStatement ParseConditionalCompilationIf(bool isElseIf)
        {
            JsContext context = m_currentToken.Clone();
            JsAstNode condition = null;
            GetNextToken();
            if (m_currentToken.Token == JsToken.LeftParenthesis)
            {
                context.UpdateWith(m_currentToken);
                GetNextToken();
                condition = ParseExpression(false);
                if (condition != null)
                {
                    context.UpdateWith(condition.Context);
                }
                else
                {
                    m_currentToken.HandleError(JsError.ExpressionExpected);
                }

                if (m_currentToken.Token == JsToken.RightParenthesis)
                {
                    context.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                else
                {
                    m_currentToken.HandleError(JsError.NoRightParenthesis);
                }
            }
            else
            {
                m_currentToken.HandleError(JsError.NoLeftParenthesis);
            }

            if (isElseIf)
            {
                return new JsConditionalCompilationElseIf(context, this)
                    {
                        Condition = condition
                    };
            }

            return new JsConditionalCompilationIf(context, this)
                {
                    Condition = condition
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseBlock
        //
        //  Block :
        //    '{' OptionalStatements '}'
        //---------------------------------------------------------------------------------------
        JsBlock ParseBlock()
        {
            m_blockType.Add(BlockType.Block);

            // set the force-braces property to true because we are assuming this is only called
            // when we encounter a left-brace and we will want to keep it going forward. If we are optimizing
            // the code, we will reset these properties as we encounter them so that unneeded curly-braces 
            // can be removed.
            JsBlock codeBlock = new JsBlock(m_currentToken.Clone(), this)
                {
                    ForceBraces = true
                };
            codeBlock.BraceOnNewLine = m_foundEndOfLine;
            GetNextToken();

            m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
            m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockNoSkipTokenSet);
            try
            {
                try
                {
                    while (JsToken.RightCurly != m_currentToken.Token)
                    {
                        try
                        {
                            // pass false because we really only want Statements, and FunctionDeclarations
                            // are technically not statements. We'll still handle them, but we'll issue a warning.
                            codeBlock.Append(ParseStatement(false));
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (exc._partiallyComputedNode != null)
                                codeBlock.Append(exc._partiallyComputedNode);
                            if (IndexOfToken(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc) == -1)
                                throw;
                        }
                    }

                    // make sure any important comments before the closing brace are kept
                    AppendImportantComments(codeBlock);
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = codeBlock;
                        throw;
                    }
                }
            }
            finally
            {
                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            codeBlock.TerminatingContext = m_currentToken.Clone();
            // update the block context
            codeBlock.Context.UpdateWith(m_currentToken);
            GetNextToken();
            return codeBlock;
        }

        //---------------------------------------------------------------------------------------
        // ParseDebuggerStatement
        //
        //  DebuggerStatement :
        //    'debugger'
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the debugger statement
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseDebuggerStatement()
        {
            // clone the current context and skip it
            var node = new JsDebuggerNode(m_currentToken.Clone(), this);
            GetNextToken();

            // this token can only be a stand-alone statement
            if (JsToken.Semicolon == m_currentToken.Token)
            {
                // add the semicolon to the cloned context and skip it
                node.TerminatingContext = m_currentToken.Clone();
                GetNextToken();
            }
            else if (m_foundEndOfLine || JsToken.RightCurly == m_currentToken.Token || JsToken.EndOfFile == m_currentToken.Token)
            {
                // semicolon insertion rules applied
                // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                // Just too common and doesn't really warrant a warning (in my opinion)
                if (JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                {
                    ReportError(JsError.SemicolonInsertion, node.Context.IfNotNull(c => c.FlattenToEnd()), true);
                }
            }
            else
            {
                // if it is anything else, it's an error
                ReportError(JsError.NoSemicolon, true);
            }

            // return the new AST object
            return node;
        }

        //---------------------------------------------------------------------------------------
        // ParseVariableStatement
        //
        //  VariableStatement :
        //    'var' VariableDeclarationList
        //    or
        //    'const' VariableDeclarationList
        //    or
        //    'let' VariableDeclarationList
        //
        //  VariableDeclarationList :
        //    VariableDeclaration |
        //    VariableDeclaration ',' VariableDeclarationList
        //
        //  VariableDeclaration :
        //    Identifier Initializer
        //
        //  Initializer :
        //    <empty> |
        //    '=' AssignmentExpression
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseVariableStatement()
        {
            // create the appropriate statement: var- or const-statement
            JsDeclaration varList;
            if (m_currentToken.Token == JsToken.Var)
            {
                varList = new JsVar(m_currentToken.Clone(), this);
            }
            else if (m_currentToken.Token == JsToken.Const || m_currentToken.Token == JsToken.Let)
            {
                if (m_currentToken.Token == JsToken.Const && m_settings.ConstStatementsMozilla)
                {
                    varList = new JsConstStatement(m_currentToken.Clone(), this);
                }
                else
                {
                    varList = new JsLexicalDeclaration(m_currentToken.Clone(), this)
                        {
                            StatementToken = m_currentToken.Token
                        };
                }
            }
            else
            {
                Debug.Fail("shouldn't get here");
                return null; 
            }

            bool single = true;
            JsAstNode vdecl = null;
            JsAstNode identInit = null;

            for (; ; )
            {
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfLineToken);
                try
                {
                    identInit = ParseIdentifierInitializer(JsToken.None);
                }
                catch (RecoveryTokenException exc)
                {
                    // an exception is passing by, possibly bringing some info, save the info if any
                    if (exc._partiallyComputedNode != null)
                    {
                        if (!single)
                        {
                            varList.Append(exc._partiallyComputedNode);
                            varList.Context.UpdateWith(exc._partiallyComputedNode.Context);
                            exc._partiallyComputedNode = varList;
                        }
                    }
                    if (IndexOfToken(NoSkipTokenSet.s_EndOfLineToken, exc) == -1)
                        throw;
                    else
                    {
                        if (single)
                            identInit = exc._partiallyComputedNode;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfLineToken);
                }

                if (identInit != null)
                {
                    vdecl = identInit;
                    varList.Append(vdecl);
                }

                if (m_currentToken.Token == JsToken.Comma)
                {
                    single = false;
                    vdecl.IfNotNull(d => d.TerminatingContext = m_currentToken.Clone());
                }
                else if (m_currentToken.Token == JsToken.Semicolon)
                {
                    varList.TerminatingContext = m_currentToken.Clone();
                    GetNextToken();
                    break;
                }
                else if (m_foundEndOfLine || m_currentToken.Token == JsToken.RightCurly || m_currentToken.Token == JsToken.EndOfFile)
                {
                    // semicolon insertion rules
                    // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                    // Just too common and doesn't really warrant a warning (in my opinion)
                    if (JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                    {
                        ReportError(JsError.SemicolonInsertion, varList.Context.IfNotNull(c => c.FlattenToEnd()), true);
                    }
                    break;
                }
                else
                {
                    ReportError(JsError.NoSemicolon, false);
                    break;
                }
            }

            if (vdecl != null)
            {
                varList.Context.UpdateWith(vdecl.Context);
            }
            return varList;
        }

        //---------------------------------------------------------------------------------------
        // ParseIdentifierInitializer
        //
        //  Does the real work of parsing a single variable declaration.
        //  inToken is JSToken.In whenever the potential expression that initialize a variable
        //  cannot contain an 'in', as in the for statement. inToken is JSToken.None otherwise
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseIdentifierInitializer(JsToken inToken)
        {
            string variableName = null;
            JsAstNode assignmentExpr = null;
            RecoveryTokenException except = null;

            GetNextToken();
            if (JsToken.Identifier != m_currentToken.Token)
            {
                String identifier = JsKeyword.CanBeIdentifier(m_currentToken.Token);
                if (null != identifier)
                {
                    variableName = identifier;
                }
                else
                {
                    // make up an identifier assume we're done with the var statement
                    if (JsScanner.IsValidIdentifier(m_currentToken.Code))
                    {
                        // it's probably just a keyword
                        ReportError(JsError.NoIdentifier, m_currentToken.Clone(), true);
                        variableName = m_currentToken.Code;
                    }
                    else
                    {
                        ReportError(JsError.NoIdentifier);
                        return null;
                    }
                }
            }
            else
            {
                variableName = m_scanner.Identifier;
            }
            JsContext idContext = m_currentToken.Clone();
            JsContext context = m_currentToken.Clone();
            JsContext assignContext = null;

            bool ccSpecialCase = false;
            bool ccOn = false;
            GetNextToken();

            m_noSkipTokenSet.Add(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
            try
            {
                if (m_currentToken.Token == JsToken.ConditionalCommentStart)
                {
                    ccSpecialCase = true;

                    GetNextToken();
                    if (m_currentToken.Token == JsToken.ConditionalCompilationOn)
                    {
                        GetNextToken();
                        if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                        {
                            // forget about it; just ignore the whole thing because it's empty
                            ccSpecialCase = false;
                        }
                        else
                        {
                            ccOn = true;
                        }
                    }
                }

                if (JsToken.Assign == m_currentToken.Token || JsToken.Equal == m_currentToken.Token)
                {
                    assignContext = m_currentToken.Clone();
                    if (JsToken.Equal == m_currentToken.Token)
                    {
                        ReportError(JsError.NoEqual, true);
                    }


                    // move past the equals sign
                    GetNextToken();
                    if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                    {
                        // so we have var id/*@ =@*/ or var id//@=<EOL>
                        // we only support the equal sign inside conditional comments IF
                        // the initializer value is there as well.
                        ccSpecialCase = false;
                        m_currentToken.HandleError(JsError.ConditionalCompilationTooComplex);
                        GetNextToken();
                    }

                    try
                    {
                        assignmentExpr = ParseExpression(true, inToken);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        assignmentExpr = exc._partiallyComputedNode;
                        throw;
                    }
                    finally
                    {
                        if (null != assignmentExpr)
                        {
                            context.UpdateWith(assignmentExpr.Context);
                        }
                    }
                }
                else if (ccSpecialCase)
                {
                    // so we have "var id /*@" or "var id //@", but the next character is NOT an equal sign.
                    // we don't support this structure, either.
                    ccSpecialCase = false;
                    m_currentToken.HandleError(JsError.ConditionalCompilationTooComplex);

                    // skip to end of conditional comment
                    while (m_currentToken.Token != JsToken.EndOfFile && m_currentToken.Token != JsToken.ConditionalCommentEnd)
                    {
                        GetNextToken();
                    }
                    GetNextToken();
                }

                // if the current token is not an end-of-conditional-comment token now,
                // then we're not in our special case scenario
                if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                {
                    GetNextToken();
                }
                else if (ccSpecialCase)
                {
                    // we have "var id/*@=expr" but the next token is not the closing comment.
                    // we don't support this structure, either.
                    ccSpecialCase = false;
                    m_currentToken.HandleError(JsError.ConditionalCompilationTooComplex);

                    // the assignment expression was apparently wiothin the conditional compilation
                    // comment, but we're going to ignore it. So clear it out.
                    assignmentExpr = null;

                    // skip to end of conditional comment
                    while (m_currentToken.Token != JsToken.EndOfFile && m_currentToken.Token != JsToken.ConditionalCommentEnd)
                    {
                        GetNextToken();
                    }
                    GetNextToken();
                }
            }
            catch (RecoveryTokenException exc)
            {
                // If the exception is in the vardecl no-skip set then we successfully
                // recovered to the end of the declaration and can just return
                // normally.  Otherwise we re-throw after constructing the partial result.  
                if (IndexOfToken(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet, exc) == -1)
                    except = exc;
            }
            finally
            {
                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
            }

            JsVariableDeclaration result = new JsVariableDeclaration(context, this)
                {
                    Identifier = variableName,
                    NameContext = idContext,
                    AssignContext = assignContext,
                    Initializer = assignmentExpr
                };

            result.IsCCSpecialCase = ccSpecialCase;
            if (ccSpecialCase)
            {
                // by default, set the flag depending on whether we encountered a @cc_on statement.
                // might be overridden by the node in analyze phase
                result.UseCCOn = ccOn;
            }

            if (null != except)
            {
                except._partiallyComputedNode = result;
                throw except;
            }

            return result;
        }

        //---------------------------------------------------------------------------------------
        // ParseIfStatement
        //
        //  IfStatement :
        //    'if' '(' Expression ')' Statement ElseStatement
        //
        //  ElseStatement :
        //    <empty> |
        //    'else' Statement
        //---------------------------------------------------------------------------------------
        private JsIfNode ParseIfStatement()
        {
            JsContext ifCtx = m_currentToken.Clone();
            JsAstNode condition = null;
            JsAstNode trueBranch = null;
            JsAstNode falseBranch = null;
            JsContext elseCtx = null;

            m_blockType.Add(BlockType.Block);
            try
            {
                // parse condition
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    if (JsToken.LeftParenthesis != m_currentToken.Token)
                        ReportError(JsError.NoLeftParenthesis);
                    GetNextToken();
                    condition = ParseExpression();

                    // parse statements
                    if (JsToken.RightParenthesis != m_currentToken.Token)
                    {
                        ifCtx.UpdateWith(condition.Context);
                        ReportError(JsError.NoRightParenthesis);
                    }
                    else
                        ifCtx.UpdateWith(m_currentToken);

                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    // make up an if condition
                    if (exc._partiallyComputedNode != null)
                        condition = exc._partiallyComputedNode;
                    else
                        condition = new JsConstantWrapper(true, JsPrimitiveType.Boolean, CurrentPositionContext(), this);

                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = null; // really not much to pass up
                        // the if condition was so bogus we do not have a chance to make an If node, give up
                        throw;
                    }
                    else
                    {
                        if (exc._token == JsToken.RightParenthesis)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // if this is an assignment, throw a warning in case the developer
                // meant to use == instead of =
                // but no warning if the condition is wrapped in parens.
                var binOp = condition as JsBinaryOperator;
                if (binOp != null && binOp.OperatorToken == JsToken.Assign)
                {
                    condition.Context.HandleError(JsError.SuspectAssignment);
                }

                m_noSkipTokenSet.Add(NoSkipTokenSet.s_IfBodyNoSkipTokenSet);
                if (JsToken.Semicolon == m_currentToken.Token)
                {
                    m_currentToken.HandleError(JsError.SuspectSemicolon);
                }
                else if (JsToken.LeftCurly != m_currentToken.Token)
                {
                    // if the statements aren't withing curly-braces, throw a possible error
                    ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                }

                try
                {
                    // parse a Statement, not a SourceElement
                    // and ignore any important comments that spring up right here.
                    trueBranch = ParseStatement(false, true);
                }
                catch (RecoveryTokenException exc)
                {
                    // make up a block for the if part
                    if (exc._partiallyComputedNode != null)
                        trueBranch = exc._partiallyComputedNode;
                    else
                        trueBranch = new JsBlock(CurrentPositionContext(), this);
                    if (IndexOfToken(NoSkipTokenSet.s_IfBodyNoSkipTokenSet, exc) == -1)
                    {
                        // we have to pass the exception to someone else, make as much as you can from the if
                        exc._partiallyComputedNode = new JsIfNode(ifCtx, this)
                            {
                                Condition = condition,
                                TrueBlock = JsAstNode.ForceToBlock(trueBranch)
                            };
                        throw;
                    }
                }
                finally
                {
                    if (trueBranch != null)
                    {
                        ifCtx.UpdateWith(trueBranch.Context);
                    }

                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_IfBodyNoSkipTokenSet);
                }

                // parse else, if any
                if (JsToken.Else == m_currentToken.Token)
                {
                    elseCtx = m_currentToken.Clone();
                    GetNextToken();
                    if (JsToken.Semicolon == m_currentToken.Token)
                    {
                        m_currentToken.HandleError(JsError.SuspectSemicolon);
                    }
                    else if (JsToken.LeftCurly != m_currentToken.Token
                      && JsToken.If != m_currentToken.Token)
                    {
                        // if the statements aren't withing curly-braces (or start another if-statement), throw a possible error
                        ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                    }

                    try
                    {
                        // parse a Statement, not a SourceElement
                        // and ignore any important comments that spring up right here.
                        falseBranch = ParseStatement(false, true);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        // make up a block for the else part
                        if (exc._partiallyComputedNode != null)
                            falseBranch = exc._partiallyComputedNode;
                        else
                            falseBranch = new JsBlock(CurrentPositionContext(), this);
                        exc._partiallyComputedNode = new JsIfNode(ifCtx, this)
                            {
                                Condition = condition,
                                TrueBlock = JsAstNode.ForceToBlock(trueBranch),
                                ElseContext = elseCtx,
                                FalseBlock = JsAstNode.ForceToBlock(falseBranch)
                            };
                        throw;
                    }
                    finally
                    {
                        if (falseBranch != null)
                        {
                            ifCtx.UpdateWith(falseBranch.Context);
                        }
                    }
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new JsIfNode(ifCtx, this)
                {
                    Condition = condition,
                    TrueBlock = JsAstNode.ForceToBlock(trueBranch),
                    ElseContext = elseCtx,
                    FalseBlock = JsAstNode.ForceToBlock(falseBranch)
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseForStatement
        //
        //  ForStatement :
        //    'for' '(' OptionalExpressionNoIn ';' OptionalExpression ';' OptionalExpression ')'
        //    'for' '(' 'var' VariableDeclarationListNoIn ';' OptionalExpression ';' OptionalExpression ')'
        //    'for' '(' LeftHandSideExpression 'in' Expression')'
        //    'for' '(' 'var' Identifier OptionalInitializerNoIn 'in' Expression')'
        //
        //  OptionalExpressionNoIn :
        //    <empty> |
        //    ExpressionNoIn // same as Expression but does not process 'in' as an operator
        //
        //  OptionalInitializerNoIn :
        //    <empty> |
        //    InitializerNoIn // same as initializer but does not process 'in' as an operator
        //---------------------------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private JsAstNode ParseForStatement()
        {
            m_blockType.Add(BlockType.Loop);
            JsAstNode forNode = null;
            try
            {
                JsContext forCtx = m_currentToken.Clone();
                GetNextToken();
                if (JsToken.LeftParenthesis != m_currentToken.Token)
                {
                    ReportError(JsError.NoLeftParenthesis);
                }

                GetNextToken();
                bool isForIn = false, recoveryInForIn = false;
                JsAstNode lhs = null, initializer = null, condOrColl = null, increment = null;
                JsContext operatorContext = null;
                JsContext separator1Context = null;
                JsContext separator2Context = null;

                try
                {
                    if (JsToken.Var == m_currentToken.Token
                        || JsToken.Let == m_currentToken.Token
                        || JsToken.Const == m_currentToken.Token)
                    {
                        isForIn = true;
                        JsDeclaration declaration;
                        if (m_currentToken.Token == JsToken.Var)
                        {
                            declaration = new JsVar(m_currentToken.Clone(), this);
                        }
                        else
                        {
                            declaration = new JsLexicalDeclaration(m_currentToken.Clone(), this)
                                {
                                    StatementToken = m_currentToken.Token
                                };
                        }
 
                        declaration.Append(ParseIdentifierInitializer(JsToken.In));

                        // a list of variable initializers is allowed only in a for(;;)
                        while (JsToken.Comma == m_currentToken.Token)
                        {
                            isForIn = false;
                            declaration.Append(ParseIdentifierInitializer(JsToken.In));
                            //initializer = new Comma(initializer.context.CombineWith(var.context), initializer, var);
                        }

                        initializer = declaration;

                        // if it could still be a for..in, now it's time to get the 'in'
                        // TODO: for ES6 might be 'of'
                        if (isForIn)
                        {
                            if (JsToken.In == m_currentToken.Token
                                || (m_currentToken.Token == JsToken.Identifier && string.CompareOrdinal(m_currentToken.Code, "of") == 0))
                            {
                                operatorContext = m_currentToken.Clone();
                                GetNextToken();
                                condOrColl = ParseExpression();
                            }
                            else
                            {
                                isForIn = false;
                            }
                        }
                    }
                    else
                    {
                        if (JsToken.Semicolon != m_currentToken.Token)
                        {
                            bool isLHS;
                            initializer = ParseUnaryExpression(out isLHS, false);
                            if (isLHS && (JsToken.In == m_currentToken.Token
                                || (m_currentToken.Token == JsToken.Identifier && string.CompareOrdinal(m_currentToken.Code, "of") == 0)))
                            {
                                isForIn = true;
                                operatorContext = m_currentToken.Clone();

                                lhs = initializer;
                                initializer = null;
                                GetNextToken();
                                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                                try
                                {
                                    condOrColl = ParseExpression();
                                }
                                catch (RecoveryTokenException exc)
                                {
                                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                                    {
                                        exc._partiallyComputedNode = null;
                                        throw;
                                    }
                                    else
                                    {
                                        if (exc._partiallyComputedNode == null)
                                            condOrColl = new JsConstantWrapper(true, JsPrimitiveType.Boolean, CurrentPositionContext(), this); // what could we put here?
                                        else
                                            condOrColl = exc._partiallyComputedNode;
                                    }
                                    if (exc._token == JsToken.RightParenthesis)
                                    {
                                        GetNextToken();
                                        recoveryInForIn = true;
                                    }
                                }
                                finally
                                {
                                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                                }
                            }
                            else
                            {
                                initializer = ParseExpression(initializer, false, isLHS, JsToken.In);
                            }
                        }
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    // error is too early abort for
                    exc._partiallyComputedNode = null;
                    throw;
                }

                // at this point we know whether or not is a for..in
                if (isForIn)
                {
                    if (!recoveryInForIn)
                    {
                        if (JsToken.RightParenthesis != m_currentToken.Token)
                            ReportError(JsError.NoRightParenthesis);
                        forCtx.UpdateWith(m_currentToken);
                        GetNextToken();
                    }

                    JsAstNode body = null;
                    // if the statements aren't withing curly-braces, throw a possible error
                    if (JsToken.LeftCurly != m_currentToken.Token)
                    {
                        ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                    }
                    try
                    {
                        // parse a Statement, not a SourceElement
                        // and ignore any important comments that spring up right here.
                        body = ParseStatement(false, true);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode == null)
                            body = new JsBlock(CurrentPositionContext(), this);
                        else
                            body = exc._partiallyComputedNode;
                        exc._partiallyComputedNode = new JsForIn(forCtx, this)
                            {
                                Variable = (lhs != null ? lhs : initializer),
                                OperatorContext = operatorContext,
                                Collection = condOrColl,
                                Body = JsAstNode.ForceToBlock(body),
                            };
                        throw;
                    }

                    // for (a in b)
                    //      lhs = a, initializer = null
                    // for (var a in b)
                    //      lhs = null, initializer = var a
                    forNode = new JsForIn(forCtx, this)
                        {
                            Variable = (lhs != null ? lhs : initializer),
                            OperatorContext = operatorContext,
                            Collection = condOrColl,
                            Body = JsAstNode.ForceToBlock(body),
                        };
                }
                else
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                    try
                    {
                        if (JsToken.Semicolon == m_currentToken.Token)
                        {
                            separator1Context = m_currentToken.Clone();
                        }
                        else
                        {
                            ReportError(JsError.NoSemicolon);
                            if (JsToken.Colon == m_currentToken.Token)
                            {
                                m_noSkipTokenSet.Add(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
                                try
                                {
                                    SkipTokensAndThrow();
                                }
                                catch (RecoveryTokenException)
                                {
                                    if (JsToken.Semicolon == m_currentToken.Token)
                                    {
                                        m_useCurrentForNext = false;
                                    }
                                    else
                                    {
                                        throw;
                                    }
                                }
                                finally
                                {
                                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_VariableDeclNoSkipTokenSet);
                                }
                            }
                        }

                        GetNextToken();
                        if (JsToken.Semicolon != m_currentToken.Token)
                        {
                            condOrColl = ParseExpression();
                            if (JsToken.Semicolon != m_currentToken.Token)
                            {
                                ReportError(JsError.NoSemicolon);
                            }
                        }

                        separator2Context = m_currentToken.Clone();
                        GetNextToken();

                        if (JsToken.RightParenthesis != m_currentToken.Token)
                        {
                            increment = ParseExpression();
                        }

                        if (JsToken.RightParenthesis != m_currentToken.Token)
                        {
                            ReportError(JsError.NoRightParenthesis);
                        }

                        forCtx.UpdateWith(m_currentToken);
                        GetNextToken();
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                        {
                            exc._partiallyComputedNode = null;
                            throw;
                        }
                        else
                        {
                            // discard any partial info, just genrate empty condition and increment and keep going
                            exc._partiallyComputedNode = null;
                            if (condOrColl == null)
                                condOrColl = new JsConstantWrapper(true, JsPrimitiveType.Boolean, CurrentPositionContext(), this);
                        }
                        if (exc._token == JsToken.RightParenthesis)
                        {
                            GetNextToken();
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                    }

                    // if this is an assignment, throw a warning in case the developer
                    // meant to use == instead of =
                    // but no warning if the condition is wrapped in parens.
                    var binOp = condOrColl as JsBinaryOperator;
                    if (binOp != null && binOp.OperatorToken == JsToken.Assign)
                    {
                        condOrColl.Context.HandleError(JsError.SuspectAssignment);
                    }

                    JsAstNode body = null;
                    // if the statements aren't withing curly-braces, throw a possible error
                    if (JsToken.LeftCurly != m_currentToken.Token)
                    {
                        ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                    }
                    try
                    {
                        // parse a Statement, not a SourceElement
                        // and ignore any important comments that spring up right here.
                        body = ParseStatement(false, true);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode == null)
                            body = new JsBlock(CurrentPositionContext(), this);
                        else
                            body = exc._partiallyComputedNode;
                        exc._partiallyComputedNode = new JsForNode(forCtx, this)
                            {
                                Initializer = initializer,
                                Separator1Context = separator1Context,
                                Condition = condOrColl,
                                Separator2Context = separator2Context,
                                Incrementer = increment,
                                Body = JsAstNode.ForceToBlock(body)
                            };
                        throw;
                    }
                    forNode = new JsForNode(forCtx, this)
                        {
                            Initializer = initializer,
                            Separator1Context = separator1Context,
                            Condition = condOrColl,
                            Separator2Context = separator2Context,
                            Incrementer = increment,
                            Body = JsAstNode.ForceToBlock(body)
                        };
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return forNode;
        }

        //---------------------------------------------------------------------------------------
        // ParseDoStatement
        //
        //  DoStatement:
        //    'do' Statement 'while' '(' Expression ')'
        //---------------------------------------------------------------------------------------
        private JsDoWhile ParseDoStatement()
        {
            var doCtx = m_currentToken.Clone();
            JsContext whileContext = null;
            JsContext terminatorContext = null;
            JsAstNode body = null;
            JsAstNode condition = null;
            m_blockType.Add(BlockType.Loop);
            try
            {
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_DoWhileBodyNoSkipTokenSet);
                // if the statements aren't withing curly-braces, throw a possible error
                if (JsToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                }
                try
                {
                    // parse a Statement, not a SourceElement
                    // and ignore any important comments that spring up right here.
                    body = ParseStatement(false, true);
                }
                catch (RecoveryTokenException exc)
                {
                    // make up a block for the do while
                    if (exc._partiallyComputedNode != null)
                        body = exc._partiallyComputedNode;
                    else
                        body = new JsBlock(CurrentPositionContext(), this);
                    if (IndexOfToken(NoSkipTokenSet.s_DoWhileBodyNoSkipTokenSet, exc) == -1)
                    {
                        // we have to pass the exception to someone else, make as much as you can from the 'do while'
                        exc._partiallyComputedNode = new JsDoWhile(doCtx.UpdateWith(CurrentPositionContext()), this)
                            {
                                Body = JsAstNode.ForceToBlock(body),
                                Condition = new JsConstantWrapper(false, JsPrimitiveType.Boolean, CurrentPositionContext(), this)
                            };
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_DoWhileBodyNoSkipTokenSet);
                }

                if (JsToken.While != m_currentToken.Token)
                {
                    ReportError(JsError.NoWhile);
                }

                whileContext = m_currentToken.Clone();
                doCtx.UpdateWith(whileContext);
                GetNextToken();

                if (JsToken.LeftParenthesis != m_currentToken.Token)
                {
                    ReportError(JsError.NoLeftParenthesis);
                }

                GetNextToken();
                // catch here so the body of the do_while is not thrown away
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    condition = ParseExpression();
                    if (JsToken.RightParenthesis != m_currentToken.Token)
                    {
                        ReportError(JsError.NoRightParenthesis);
                        doCtx.UpdateWith(condition.Context);
                    }
                    else
                    {
                        doCtx.UpdateWith(m_currentToken);
                    }

                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    // make up a condition
                    if (exc._partiallyComputedNode != null)
                        condition = exc._partiallyComputedNode;
                    else
                        condition = new JsConstantWrapper(false, JsPrimitiveType.Boolean, CurrentPositionContext(), this);

                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = new JsDoWhile(doCtx, this)
                            {
                                Body = JsAstNode.ForceToBlock(body),
                                WhileContext = whileContext,
                                Condition = condition
                            };
                        throw;
                    }
                    else
                    {
                        if (JsToken.RightParenthesis == m_currentToken.Token)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }
                if (JsToken.Semicolon == m_currentToken.Token)
                {
                    // JScript 5 allowed statements like
                    //   do{print(++x)}while(x<10) print(0)
                    // even though that does not strictly follow the automatic semicolon insertion
                    // rules for the required semi after the while().  For backwards compatibility
                    // we should continue to support this.
                    terminatorContext = m_currentToken.Clone();
                    GetNextToken();
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            // if this is an assignment, throw a warning in case the developer
            // meant to use == instead of =
            // but no warning if the condition is wrapped in parens.
            var binOp = condition as JsBinaryOperator;
            if (binOp != null && binOp.OperatorToken == JsToken.Assign)
            {
                condition.Context.HandleError(JsError.SuspectAssignment);
            }

            return new JsDoWhile(doCtx, this)
                {
                    Body = JsAstNode.ForceToBlock(body),
                    WhileContext = whileContext,
                    Condition = condition,
                    TerminatingContext = terminatorContext
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseWhileStatement
        //
        //  WhileStatement :
        //    'while' '(' Expression ')' Statement
        //---------------------------------------------------------------------------------------
        private JsWhileNode ParseWhileStatement()
        {
            JsContext whileCtx = m_currentToken.Clone();
            JsAstNode condition = null;
            JsAstNode body = null;
            m_blockType.Add(BlockType.Loop);
            try
            {
                GetNextToken();
                if (JsToken.LeftParenthesis != m_currentToken.Token)
                {
                    ReportError(JsError.NoLeftParenthesis);
                }
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    condition = ParseExpression();
                    if (JsToken.RightParenthesis != m_currentToken.Token)
                    {
                        ReportError(JsError.NoRightParenthesis);
                        whileCtx.UpdateWith(condition.Context);
                    }
                    else
                        whileCtx.UpdateWith(m_currentToken);

                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        // abort the while there is really no much to do here
                        exc._partiallyComputedNode = null;
                        throw;
                    }
                    else
                    {
                        // make up a condition
                        if (exc._partiallyComputedNode != null)
                            condition = exc._partiallyComputedNode;
                        else
                            condition = new JsConstantWrapper(false, JsPrimitiveType.Boolean, CurrentPositionContext(), this);

                        if (JsToken.RightParenthesis == m_currentToken.Token)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // if this is an assignment, throw a warning in case the developer
                // meant to use == instead of =
                // but no warning if the condition is wrapped in parens.
                var binOp = condition as JsBinaryOperator;
                if (binOp != null && binOp.OperatorToken == JsToken.Assign)
                {
                    condition.Context.HandleError(JsError.SuspectAssignment);
                }

                // if the statements aren't withing curly-braces, throw a possible error
                if (JsToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                }
                try
                {
                    // parse a Statement, not a SourceElement
                    // and ignore any important comments that spring up right here.
                    body = ParseStatement(false, true);
                }
                catch (RecoveryTokenException exc)
                {
                    if (exc._partiallyComputedNode != null)
                        body = exc._partiallyComputedNode;
                    else
                        body = new JsBlock(CurrentPositionContext(), this);

                    exc._partiallyComputedNode = new JsWhileNode(whileCtx, this)
                        {
                            Condition = condition,
                            Body = JsAstNode.ForceToBlock(body)
                        };
                    throw;
                }

            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new JsWhileNode(whileCtx, this)
                {
                    Condition = condition,
                    Body = JsAstNode.ForceToBlock(body)
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseContinueStatement
        //
        //  ContinueStatement :
        //    'continue' OptionalLabel
        //
        //  OptionalLabel :
        //    <empty> |
        //    Identifier
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the continue statement
        //---------------------------------------------------------------------------------------
        private JsContinueNode ParseContinueStatement()
        {
            var continueNode = new JsContinueNode(m_currentToken.Clone(), this);
            GetNextToken();

            var blocks = 0;
            string label = null;
            if (!m_foundEndOfLine && (JsToken.Identifier == m_currentToken.Token || (label = JsKeyword.CanBeIdentifier(m_currentToken.Token)) != null))
            {
                continueNode.UpdateWith(m_currentToken);
                continueNode.LabelContext = m_currentToken.Clone();
                continueNode.Label = label ?? m_scanner.Identifier;

                // get the label block
                if (!m_labelTable.ContainsKey(continueNode.Label))
                {
                    // the label does not exist. Continue anyway
                    ReportError(JsError.NoLabel, true);
                }
                else
                {
                    var labelInfo = m_labelTable[continueNode.Label];
                    continueNode.NestLevel = labelInfo.NestLevel;

                    blocks = labelInfo.BlockIndex;
                    if (m_blockType[blocks] != BlockType.Loop)
                    {
                        ReportError(JsError.BadContinue, continueNode.Context, true);
                    }
                }

                GetNextToken();
            }
            else
            {
                blocks = m_blockType.Count - 1;
                while (blocks >= 0 && m_blockType[blocks] != BlockType.Loop) blocks--;
                if (blocks < 0)
                {
                    // the continue is malformed. Continue as if there was no continue at all
                    ReportError(JsError.BadContinue, continueNode.Context, true);
                    return null;
                }
            }

            if (JsToken.Semicolon == m_currentToken.Token)
            {
                continueNode.TerminatingContext = m_currentToken.Clone();
                GetNextToken();
            }
            else if (m_foundEndOfLine || m_currentToken.Token == JsToken.RightCurly || m_currentToken.Token == JsToken.EndOfFile)
            {
                // semicolon insertion rules
                // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                // Just too common and doesn't really warrant a warning (in my opinion)
                if (JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                {
                    ReportError(JsError.SemicolonInsertion, continueNode.Context.IfNotNull(c => c.FlattenToEnd()), true);
                }
            }
            else
            {
                ReportError(JsError.NoSemicolon, false);
            }

            // must ignore the Finally block
            var finallyNum = 0;
            for (int i = blocks, n = m_blockType.Count; i < n; i++)
            {
                if (m_blockType[i] == BlockType.Finally)
                {
                    blocks++;
                    finallyNum++;
                }
            }

            if (finallyNum > m_finallyEscaped)
            {
                m_finallyEscaped = finallyNum;
            }

            return continueNode;
        }

        //---------------------------------------------------------------------------------------
        // ParseBreakStatement
        //
        //  BreakStatement :
        //    'break' OptionalLabel
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the break statement.
        //---------------------------------------------------------------------------------------
        private JsBreak ParseBreakStatement()
        {
            var breakNode = new JsBreak(m_currentToken.Clone(), this);
            GetNextToken();

            var blocks = 0;
            string label = null;
            if (!m_foundEndOfLine && (JsToken.Identifier == m_currentToken.Token || (label = JsKeyword.CanBeIdentifier(m_currentToken.Token)) != null))
            {
                breakNode.UpdateWith(m_currentToken);
                breakNode.LabelContext = m_currentToken.Clone();
                breakNode.Label = label ?? m_scanner.Identifier;

                // get the label block
                if (!m_labelTable.ContainsKey(breakNode.Label))
                {
                    // as if it was a non label case
                    ReportError(JsError.NoLabel, true);
                }
                else
                {
                    LabelInfo labelInfo = m_labelTable[breakNode.Label];
                    breakNode.NestLevel = labelInfo.NestLevel;
                    blocks = labelInfo.BlockIndex - 1; // the outer block
                    Debug.Assert(m_blockType[blocks] != BlockType.Finally);
                }

                GetNextToken();
            }
            else
            {
                blocks = m_blockType.Count - 1;
                // search for an enclosing loop, if there is no loop it is an error
                while ((m_blockType[blocks] == BlockType.Block || m_blockType[blocks] == BlockType.Finally) && --blocks >= 0) ;
                --blocks;
                if (blocks < 0)
                {
                    ReportError(JsError.BadBreak, breakNode.Context, true);
                    return null;
                }
            }

            if (JsToken.Semicolon == m_currentToken.Token)
            {
                breakNode.TerminatingContext = m_currentToken.Clone();
                GetNextToken();
            }
            else if (m_foundEndOfLine || m_currentToken.Token == JsToken.RightCurly || m_currentToken.Token == JsToken.EndOfFile)
            {
                // semicolon insertion rules
                // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                // Just too common and doesn't really warrant a warning (in my opinion)
                if (JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                {
                    ReportError(JsError.SemicolonInsertion, breakNode.Context.IfNotNull(c => c.FlattenToEnd()), true);
                }
            }
            else
            {
                ReportError(JsError.NoSemicolon, false);
            }

            // must ignore the Finally block
            var finallyNum = 0;
            for (int i = blocks, n = m_blockType.Count; i < n; i++)
            {
                if (m_blockType[i] == BlockType.Finally)
                {
                    blocks++;
                    finallyNum++;
                }
            }

            if (finallyNum > m_finallyEscaped)
            {
                m_finallyEscaped = finallyNum;
            }

            return breakNode;
        }

        //---------------------------------------------------------------------------------------
        // ParseReturnStatement
        //
        //  ReturnStatement :
        //    'return' Expression
        //
        // This function may return a null AST under error condition. The caller should handle
        // that case.
        // Regardless of error conditions, on exit the parser points to the first token after
        // the return statement.
        //---------------------------------------------------------------------------------------
        private JsReturnNode ParseReturnStatement()
        {
            var returnNode = new JsReturnNode(m_currentToken.Clone(), this);
            GetNextToken();

            if (!m_foundEndOfLine)
            {
                if (JsToken.Semicolon != m_currentToken.Token && JsToken.RightCurly != m_currentToken.Token)
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    try
                    {
                        returnNode.Operand = ParseExpression();
                    }
                    catch (RecoveryTokenException exc)
                    {
                        returnNode.Operand = exc._partiallyComputedNode;
                        if (IndexOfToken(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet, exc) == -1)
                        {
                            exc._partiallyComputedNode = returnNode;
                            throw;
                        }
                    }
                    finally
                    {
                        if (returnNode.Operand != null)
                        {
                            returnNode.UpdateWith(returnNode.Operand.Context);
                        }

                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    }
                }

                if (JsToken.Semicolon == m_currentToken.Token)
                {
                    returnNode.TerminatingContext = m_currentToken.Clone();
                    GetNextToken();
                }
                else if (m_foundEndOfLine || m_currentToken.Token == JsToken.RightCurly || m_currentToken.Token == JsToken.EndOfFile)
                {
                    // semicolon insertion rules
                    // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                    // Just too common and doesn't really warrant a warning (in my opinion)
                    if (JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                    {
                        ReportError(JsError.SemicolonInsertion, returnNode.Context.IfNotNull(c => c.FlattenToEnd()), true);
                    }
                }
                else
                {
                    ReportError(JsError.NoSemicolon, false);
                }
            }

            return returnNode;
        }

        //---------------------------------------------------------------------------------------
        // ParseWithStatement
        //
        //  WithStatement :
        //    'with' '(' Expression ')' Statement
        //---------------------------------------------------------------------------------------
        private JsWithNode ParseWithStatement()
        {
            JsContext withCtx = m_currentToken.Clone();
            JsAstNode obj = null;
            JsBlock block = null;
            m_blockType.Add(BlockType.Block);
            try
            {
                GetNextToken();
                if (JsToken.LeftParenthesis != m_currentToken.Token)
                    ReportError(JsError.NoLeftParenthesis);
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                try
                {
                    obj = ParseExpression();
                    if (JsToken.RightParenthesis != m_currentToken.Token)
                    {
                        withCtx.UpdateWith(obj.Context);
                        ReportError(JsError.NoRightParenthesis);
                    }
                    else
                        withCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                    {
                        // give up
                        exc._partiallyComputedNode = null;
                        throw;
                    }
                    else
                    {
                        if (exc._partiallyComputedNode == null)
                            obj = new JsConstantWrapper(true, JsPrimitiveType.Boolean, CurrentPositionContext(), this);
                        else
                            obj = exc._partiallyComputedNode;
                        withCtx.UpdateWith(obj.Context);

                        if (exc._token == JsToken.RightParenthesis)
                            GetNextToken();
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // if the statements aren't withing curly-braces, throw a possible error
                if (JsToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JsError.StatementBlockExpected, CurrentPositionContext(), true);
                }

                try
                {
                    // parse a Statement, not a SourceElement
                    // and ignore any important comments that spring up right here.
                    JsAstNode statement = ParseStatement(false, true);

                    // but make sure we save it as a block
                    block = statement as JsBlock;
                    if (block == null)
                    {
                        block = new JsBlock(statement.Context, this);
                        block.Append(statement);
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (exc._partiallyComputedNode == null)
                    {
                        block = new JsBlock(CurrentPositionContext(), this);
                    }
                    else
                    {
                        block = exc._partiallyComputedNode as JsBlock;
                        if (block == null)
                        {
                            block = new JsBlock(exc._partiallyComputedNode.Context, this);
                            block.Append(exc._partiallyComputedNode);
                        }
                    }
                    exc._partiallyComputedNode = new JsWithNode(withCtx, this)
                        {
                            WithObject = obj,
                            Body = block
                        };
                    throw;
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new JsWithNode(withCtx, this)
                {
                    WithObject = obj,
                    Body = block
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseSwitchStatement
        //
        //  SwitchStatement :
        //    'switch' '(' Expression ')' '{' CaseBlock '}'
        //
        //  CaseBlock :
        //    CaseList DefaultCaseClause CaseList
        //
        //  CaseList :
        //    <empty> |
        //    CaseClause CaseList
        //
        //  CaseClause :
        //    'case' Expression ':' OptionalStatements
        //
        //  DefaultCaseClause :
        //    <empty> |
        //    'default' ':' OptionalStatements
        //---------------------------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private JsAstNode ParseSwitchStatement()
        {
            JsContext switchCtx = m_currentToken.Clone();
            JsAstNode expr = null;
            JsAstNodeList cases = null;
            var braceOnNewLine = false;
            JsContext braceContext = null;
            m_blockType.Add(BlockType.Switch);
            try
            {
                // read switch(expr)
                GetNextToken();
                if (JsToken.LeftParenthesis != m_currentToken.Token)
                    ReportError(JsError.NoLeftParenthesis);
                GetNextToken();
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                try
                {
                    expr = ParseExpression();

                    if (JsToken.RightParenthesis != m_currentToken.Token)
                    {
                        ReportError(JsError.NoRightParenthesis);
                    }

                    GetNextToken();
                    if (JsToken.LeftCurly != m_currentToken.Token)
                    {
                        ReportError(JsError.NoLeftCurly);
                    }

                    braceOnNewLine = m_foundEndOfLine;
                    braceContext = m_currentToken.Clone();
                    GetNextToken();

                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1
                          && IndexOfToken(NoSkipTokenSet.s_SwitchNoSkipTokenSet, exc) == -1)
                    {
                        // give up
                        exc._partiallyComputedNode = null;
                        throw;
                    }
                    else
                    {
                        if (exc._partiallyComputedNode == null)
                            expr = new JsConstantWrapper(true, JsPrimitiveType.Boolean, CurrentPositionContext(), this);
                        else
                            expr = exc._partiallyComputedNode;

                        if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) != -1)
                        {
                            if (exc._token == JsToken.RightParenthesis)
                                GetNextToken();

                            if (JsToken.LeftCurly != m_currentToken.Token)
                            {
                                ReportError(JsError.NoLeftCurly);
                            }
                            braceOnNewLine = m_foundEndOfLine;
                            braceContext = m_currentToken.Clone();
                            GetNextToken();
                        }

                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                }

                // parse the switch body
                cases = new JsAstNodeList(CurrentPositionContext(), this);
                bool defaultStatement = false;
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                try
                {
                    while (JsToken.RightCurly != m_currentToken.Token)
                    {
                        JsSwitchCase caseClause = null;
                        JsAstNode caseValue = null;
                        var caseCtx = m_currentToken.Clone();
                        JsContext colonContext = null;
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_CaseNoSkipTokenSet);
                        try
                        {
                            if (JsToken.Case == m_currentToken.Token)
                            {
                                // get the case
                                GetNextToken();
                                caseValue = ParseExpression();
                            }
                            else if (JsToken.Default == m_currentToken.Token)
                            {
                                // get the default
                                if (defaultStatement)
                                {
                                    // we report an error but we still accept the default
                                    ReportError(JsError.DupDefault, true);
                                }
                                else
                                {
                                    defaultStatement = true;
                                }
                                GetNextToken();
                            }
                            else
                            {
                                // This is an error, there is no case or default. Assume a default was missing and keep going
                                defaultStatement = true;
                                ReportError(JsError.BadSwitch);
                            }

                            if (JsToken.Colon != m_currentToken.Token)
                            {
                                ReportError(JsError.NoColon);
                            }
                            else
                            {
                                colonContext = m_currentToken.Clone();
                            }

                            // read the statements inside the case or default
                            GetNextToken();
                        }
                        catch (RecoveryTokenException exc)
                        {
                            // right now we can only get here for the 'case' statement
                            if (IndexOfToken(NoSkipTokenSet.s_CaseNoSkipTokenSet, exc) == -1)
                            {
                                // ignore the current case or default
                                exc._partiallyComputedNode = null;
                                throw;
                            }
                            else
                            {
                                caseValue = exc._partiallyComputedNode;

                                if (exc._token == JsToken.Colon)
                                {
                                    GetNextToken();
                                }
                            }
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_CaseNoSkipTokenSet);
                        }

                        m_blockType.Add(BlockType.Block);
                        try
                        {
                            var statements = new JsBlock(m_currentToken.Clone(), this);
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                            try
                            {
                                while (JsToken.RightCurly != m_currentToken.Token && JsToken.Case != m_currentToken.Token && JsToken.Default != m_currentToken.Token)
                                {
                                    try
                                    {
                                        // parse a Statement, not a SourceElement
                                        statements.Append(ParseStatement(false));
                                    }
                                    catch (RecoveryTokenException exc)
                                    {
                                        if (exc._partiallyComputedNode != null)
                                        {
                                            statements.Append(exc._partiallyComputedNode);
                                            exc._partiallyComputedNode = null;
                                        }

                                        if (IndexOfToken(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc) == -1)
                                        {
                                            throw;
                                        }
                                    }
                                }
                            }
                            catch (RecoveryTokenException exc)
                            {
                                if (IndexOfToken(NoSkipTokenSet.s_SwitchNoSkipTokenSet, exc) == -1)
                                {
                                    caseClause = new JsSwitchCase(caseCtx, this)
                                        {
                                            CaseValue = caseValue,
                                            ColonContext = colonContext,
                                            Statements = statements
                                        };
                                    cases.Append(caseClause);
                                    throw;
                                }
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_SwitchNoSkipTokenSet);
                            }

                            caseCtx.UpdateWith(statements.Context);
                            caseClause = new JsSwitchCase(caseCtx, this)
                                {
                                    CaseValue = caseValue,
                                    ColonContext = colonContext,
                                    Statements = statements
                                };
                            cases.Append(caseClause);
                        }
                        finally
                        {
                            m_blockType.RemoveAt(m_blockType.Count - 1);
                        }
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockNoSkipTokenSet, exc) == -1)
                    {
                        //save what you can a rethrow
                        switchCtx.UpdateWith(CurrentPositionContext());
                        exc._partiallyComputedNode = new JsSwitch(switchCtx, this)
                            {
                                Expression = expr,
                                BraceContext = braceContext,
                                Cases = cases,
                                BraceOnNewLine = braceOnNewLine
                            };
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                }
                switchCtx.UpdateWith(m_currentToken);
                GetNextToken();
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            return new JsSwitch(switchCtx, this)
                {
                    Expression = expr,
                    BraceContext = braceContext,
                    Cases = cases,
                    BraceOnNewLine = braceOnNewLine
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseThrowStatement
        //
        //  ThrowStatement :
        //    throw |
        //    throw Expression
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseThrowStatement()
        {
            var throwNode = new JsThrowNode(m_currentToken.Clone(), this);
            GetNextToken();

            if (!m_foundEndOfLine)
            {
                if (JsToken.Semicolon != m_currentToken.Token)
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    try
                    {
                        throwNode.Operand = ParseExpression();
                    }
                    catch (RecoveryTokenException exc)
                    {
                        throwNode.Operand = exc._partiallyComputedNode;
                        if (IndexOfToken(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet, exc) == -1)
                        {
                            exc._partiallyComputedNode = throwNode;
                            throw;
                        }
                    }
                    finally
                    {
                        if (throwNode.Operand != null)
                        {
                            throwNode.UpdateWith(throwNode.Operand.Context);
                        }

                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_EndOfStatementNoSkipTokenSet);
                    }
                }

                if (m_currentToken.Token == JsToken.Semicolon)
                {
                    throwNode.TerminatingContext = m_currentToken.Clone();
                    GetNextToken();
                }
                else if (m_foundEndOfLine || m_currentToken.Token == JsToken.RightCurly || m_currentToken.Token == JsToken.EndOfFile)
                {
                    // semicolon insertion rules
                    // a right-curly or an end of line is something we don't WANT to throw a warning for. 
                    // Just too common and doesn't really warrant a warning (in my opinion)
                    if (JsToken.RightCurly != m_currentToken.Token && JsToken.EndOfFile != m_currentToken.Token)
                    {
                        ReportError(JsError.SemicolonInsertion, throwNode.Context.IfNotNull(c => c.FlattenToEnd()), true);
                    }
                }
                else
                {
                    ReportError(JsError.NoSemicolon, false);
                }
            }

            return throwNode;
        }

        //---------------------------------------------------------------------------------------
        // ParseTryStatement
        //
        //  TryStatement :
        //    'try' Block Catch Finally
        //
        //  Catch :
        //    <empty> | 'catch' '(' Identifier ')' Block
        //
        //  Finally :
        //    <empty> |
        //    'finally' Block
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseTryStatement()
        {
            JsContext tryCtx = m_currentToken.Clone();
            JsContext catchContext = null;
            JsContext finallyContext = null;
            JsBlock body = null;
            JsContext idContext = null;
            JsBlock handler = null;
            JsBlock finally_block = null;
            RecoveryTokenException excInFinally = null;
            m_blockType.Add(BlockType.Block);
            try
            {
                bool catchOrFinally = false;
                GetNextToken();
                if (JsToken.LeftCurly != m_currentToken.Token)
                {
                    ReportError(JsError.NoLeftCurly);
                }
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_NoTrySkipTokenSet);
                try
                {
                    body = ParseBlock();
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_NoTrySkipTokenSet, exc) == -1)
                    {
                        // do nothing and just return the containing block, if any
                        throw;
                    }
                    else
                    {
                        body = exc._partiallyComputedNode as JsBlock;
                        if (body == null)
                        {
                            body = new JsBlock(exc._partiallyComputedNode.Context, this);
                            body.Append(exc._partiallyComputedNode);
                        }
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_NoTrySkipTokenSet);
                }
                if (JsToken.Catch == m_currentToken.Token)
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_NoTrySkipTokenSet);
                    try
                    {
                        catchOrFinally = true;
                        catchContext = m_currentToken.Clone();
                        GetNextToken();
                        if (JsToken.LeftParenthesis != m_currentToken.Token)
                        {
                            ReportError(JsError.NoLeftParenthesis);
                        }

                        GetNextToken();
                        if (JsToken.Identifier != m_currentToken.Token)
                        {
                            string identifier = JsKeyword.CanBeIdentifier(m_currentToken.Token);
                            if (null != identifier)
                            {
                                idContext = m_currentToken.Clone();
                            }
                            else
                            {
                                ReportError(JsError.NoIdentifier);
                            }
                        }
                        else
                        {
                            idContext = m_currentToken.Clone();
                        }

                        GetNextToken();
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                        try
                        {
                            if (JsToken.RightParenthesis != m_currentToken.Token)
                            {
                                ReportError(JsError.NoRightParenthesis);
                            }
                            GetNextToken();
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (IndexOfToken(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet, exc) == -1)
                            {
                                exc._partiallyComputedNode = null;
                                // rethrow
                                throw;
                            }
                            else
                            {
                                if (m_currentToken.Token == JsToken.RightParenthesis)
                                {
                                    GetNextToken();
                                }
                            }
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockConditionNoSkipTokenSet);
                        }

                        if (JsToken.LeftCurly != m_currentToken.Token)
                        {
                            ReportError(JsError.NoLeftCurly);
                        }

                        // parse the block
                        handler = ParseBlock();

                        tryCtx.UpdateWith(handler.Context);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode == null)
                        {
                            handler = new JsBlock(CurrentPositionContext(), this);
                        }
                        else
                        {
                            handler = exc._partiallyComputedNode as JsBlock;
                            if (handler == null)
                            {
                                handler = new JsBlock(exc._partiallyComputedNode.Context, this);
                                handler.Append(exc._partiallyComputedNode);
                            }
                        }
                        if (IndexOfToken(NoSkipTokenSet.s_NoTrySkipTokenSet, exc) == -1)
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_NoTrySkipTokenSet);
                    }
                }

                try
                {
                    if (JsToken.Finally == m_currentToken.Token)
                    {
                        finallyContext = m_currentToken.Clone();
                        GetNextToken();
                        m_blockType.Add(BlockType.Finally);
                        try
                        {
                            finally_block = ParseBlock();
                            catchOrFinally = true;
                        }
                        finally
                        {
                            m_blockType.RemoveAt(m_blockType.Count - 1);
                        }
                        tryCtx.UpdateWith(finally_block.Context);
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    excInFinally = exc; // thrown later so we can execute code below
                }

                if (!catchOrFinally)
                {
                    ReportError(JsError.NoCatch, true);
                    finally_block = new JsBlock(CurrentPositionContext(), this); // make a dummy empty block
                }
            }
            finally
            {
                m_blockType.RemoveAt(m_blockType.Count - 1);
            }

            JsParameterDeclaration catchParameter = null;
            if (idContext != null)
            {
                catchParameter = new JsParameterDeclaration(idContext, this)
                    {
                        Name = idContext.Code
                    };
            }

            if (excInFinally != null)
            {
                excInFinally._partiallyComputedNode = new JsTryNode(tryCtx, this)
                    {
                        TryBlock = body,
                        CatchContext = catchContext,
                        CatchParameter = catchParameter,
                        CatchBlock = handler,
                        FinallyContext = finallyContext,
                        FinallyBlock = finally_block
                    };
                throw excInFinally;
            }
            return new JsTryNode(tryCtx, this)
                {
                    TryBlock = body,
                    CatchContext = catchContext,
                    CatchParameter = catchParameter,
                    CatchBlock = handler,
                    FinallyContext = finallyContext,
                    FinallyBlock = finally_block
                };
        }

        //---------------------------------------------------------------------------------------
        // ParseFunction
        //
        //  FunctionDeclaration :
        //    VisibilityModifier 'function' Identifier '('
        //                          FormalParameterList ')' '{' FunctionBody '}'
        //
        //  FormalParameterList :
        //    <empty> |
        //    IdentifierList Identifier
        //
        //  IdentifierList :
        //    <empty> |
        //    Identifier, IdentifierList
        //---------------------------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private JsFunctionObject ParseFunction(JsFunctionType functionType, JsContext fncCtx)
        {
            JsLookup name = null;
            JsAstNodeList formalParameters = null;
            JsBlock body = null;
            bool inExpression = (functionType == JsFunctionType.Expression);
            JsContext paramsContext = null;

            GetNextToken();

            // get the function name or make an anonymous function if in expression "position"
            if (JsToken.Identifier == m_currentToken.Token)
            {
                name = new JsLookup(m_currentToken.Clone(), this)
                    {
                        Name = m_scanner.Identifier
                    };
                GetNextToken();
            }
            else
            {
                string identifier = JsKeyword.CanBeIdentifier(m_currentToken.Token);
                if (null != identifier)
                {
                    name = new JsLookup(m_currentToken.Clone(), this)
                        {
                            Name = identifier
                        };
                    GetNextToken();
                }
                else
                {
                    if (!inExpression)
                    {
                        // if this isn't a function expression, then we need to throw an error because
                        // function DECLARATIONS always need a valid identifier name
                        ReportError(JsError.NoIdentifier, m_currentToken.Clone(), true);

                        // BUT if the current token is a left paren, we don't want to use it as the name.
                        // (fix for issue #14152)
                        if (m_currentToken.Token != JsToken.LeftParenthesis
                            && m_currentToken.Token != JsToken.LeftCurly)
                        {
                            identifier = m_currentToken.Code;
                            name = new JsLookup(CurrentPositionContext(), this)
                                {
                                    Name = identifier
                                };
                            GetNextToken();
                        }
                    }
                }
            }

            // make a new state and save the old one
            List<BlockType> blockType = m_blockType;
            m_blockType = new List<BlockType>(16);
            Dictionary<string, LabelInfo> labelTable = m_labelTable;
            m_labelTable = new Dictionary<string, LabelInfo>();

            try
            {
                // get the formal parameters
                if (JsToken.LeftParenthesis != m_currentToken.Token)
                {
                    // we expect a left paren at this point for standard cross-browser support.
                    // BUT -- some versions of IE allow an object property expression to be a function name, like window.onclick. 
                    // we still want to throw the error, because it syntax errors on most browsers, but we still want to
                    // be able to parse it and return the intended results. 
                    // Skip to the open paren and use whatever is in-between as the function name. Doesn't matter that it's 
                    // an invalid identifier; it won't be accessible as a valid field anyway.
                    bool expandedIndentifier = false;
                    while (m_currentToken.Token != JsToken.LeftParenthesis
                        && m_currentToken.Token != JsToken.LeftCurly
                        && m_currentToken.Token != JsToken.Semicolon
                        && m_currentToken.Token != JsToken.EndOfFile)
                    {
                        name.Context.UpdateWith(m_currentToken);
                        GetNextToken();
                        expandedIndentifier = true;
                    }

                    // if we actually expanded the identifier context, then we want to report that
                    // the function name needs to be an identifier. Otherwise we didn't expand the 
                    // name, so just report that we expected an open paren at this point.
                    if (expandedIndentifier)
                    {
                        name.Name = name.Context.Code;
                        name.Context.HandleError(JsError.FunctionNameMustBeIdentifier, false);
                    }
                    else
                    {
                        ReportError(JsError.NoLeftParenthesis, true);
                    }
                }

                if (m_currentToken.Token == JsToken.LeftParenthesis)
                {
                    // create the parameter list
                    formalParameters = new JsAstNodeList(m_currentToken.Clone(), this);
                    paramsContext = m_currentToken.Clone();

                    // skip the open paren
                    GetNextToken();

                    // create the list of arguments and update the context
                    while (JsToken.RightParenthesis != m_currentToken.Token)
                    {
                        String id = null;
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_FunctionDeclNoSkipTokenSet);
                        try
                        {
                            JsParameterDeclaration paramDecl = null;
                            if (JsToken.Identifier != m_currentToken.Token && (id = JsKeyword.CanBeIdentifier(m_currentToken.Token)) == null)
                            {
                                if (JsToken.LeftCurly == m_currentToken.Token)
                                {
                                    ReportError(JsError.NoRightParenthesis);
                                    break;
                                }
                                else if (JsToken.Comma == m_currentToken.Token)
                                {
                                    // We're missing an argument (or previous argument was malformed and
                                    // we skipped to the comma.)  Keep trying to parse the argument list --
                                    // we will skip the comma below.
                                    ReportError(JsError.SyntaxError, true);
                                }
                                else
                                {
                                    ReportError(JsError.SyntaxError, true);
                                    SkipTokensAndThrow();
                                }
                            }
                            else
                            {
                                if (null == id)
                                {
                                    id = m_scanner.Identifier;
                                }

                                paramDecl = new JsParameterDeclaration(m_currentToken.Clone(), this)
                                    {
                                        Name = id,
                                        Position = formalParameters.Count
                                    };
                                formalParameters.Append(paramDecl);
                                GetNextToken();
                            }

                            // got an arg, it should be either a ',' or ')'
                            if (JsToken.RightParenthesis == m_currentToken.Token)
                            {
                                break;
                            }
                            else if (JsToken.Comma == m_currentToken.Token)
                            {
                                // append the comma context as the terminator for the parameter
                                paramDecl.IfNotNull(p => p.TerminatingContext = m_currentToken.Clone());
                            }
                            else
                            {
                                // deal with error in some "intelligent" way
                                if (JsToken.LeftCurly == m_currentToken.Token)
                                {
                                    ReportError(JsError.NoRightParenthesis);
                                    break;
                                }
                                else
                                {
                                    if (JsToken.Identifier == m_currentToken.Token)
                                    {
                                        // it's possible that the guy was writing the type in C/C++ style (i.e. int x)
                                        ReportError(JsError.NoCommaOrTypeDefinitionError);
                                    }
                                    else
                                        ReportError(JsError.NoComma);
                                }
                            }

                            GetNextToken();
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (IndexOfToken(NoSkipTokenSet.s_FunctionDeclNoSkipTokenSet, exc) == -1)
                                throw;
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_FunctionDeclNoSkipTokenSet);
                        }
                    }

                    fncCtx.UpdateWith(m_currentToken);
                    GetNextToken();
                }

                // read the function body of non-abstract functions.
                if (JsToken.LeftCurly != m_currentToken.Token)
                    ReportError(JsError.NoLeftCurly, true);

                m_blockType.Add(BlockType.Block);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                try
                {
                    // parse the block locally to get the exact end of function
                    body = new JsBlock(m_currentToken.Clone(), this);
                    body.BraceOnNewLine = m_foundEndOfLine;
                    GetNextToken();

                    var possibleDirectivePrologue = true;
                    while (JsToken.RightCurly != m_currentToken.Token)
                    {
                        try
                        {
                            // function body's are SourceElements (Statements + FunctionDeclarations)
                            var statement = ParseStatement(true);
                            if (possibleDirectivePrologue)
                            {
                                var constantWrapper = statement as JsConstantWrapper;
                                if (constantWrapper != null && constantWrapper.PrimitiveType == JsPrimitiveType.String)
                                {
                                    // if it's already a directive prologues, we're good to go
                                    if (!(constantWrapper is JsDirectivePrologue))
                                    {
                                        // make the statement a directive prologue instead of a constant wrapper
                                        statement = new JsDirectivePrologue(constantWrapper.Value.ToString(), constantWrapper.Context, constantWrapper.Parser)
                                            {
                                                MayHaveIssues = constantWrapper.MayHaveIssues
                                            };
                                    }
                                }
                                else if (!m_newModule)
                                {
                                    // no longer considering constant wrappers
                                    possibleDirectivePrologue = false;
                                }
                            }
                            else if (m_newModule)
                            {
                                // we scanned into a new module -- we might find directive prologues again
                                possibleDirectivePrologue = true;
                            }

                            // add it to the body
                            body.Append(statement);
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (exc._partiallyComputedNode != null)
                            {
                                body.Append(exc._partiallyComputedNode);
                            }
                            if (IndexOfToken(NoSkipTokenSet.s_StartStatementNoSkipTokenSet, exc) == -1)
                                throw;
                        }
                    }

                    // make sure any important comments before the closing brace are kept
                    AppendImportantComments(body);

                    body.Context.UpdateWith(m_currentToken);
                    fncCtx.UpdateWith(m_currentToken);
                }
                catch (EndOfStreamException)
                {
                    // if we get an EOF here, we never had a chance to find the closing curly-brace
                    fncCtx.HandleError(JsError.UnclosedFunction, true);
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_BlockNoSkipTokenSet, exc) == -1)
                    {
                        exc._partiallyComputedNode = new JsFunctionObject(fncCtx, this)
                            {
                                FunctionType = (inExpression ? JsFunctionType.Expression : JsFunctionType.Declaration),
                                IdContext = name.IfNotNull(n => n.Context),
                                Name = name.IfNotNull(n => n.Name),
                                ParameterDeclarations = formalParameters,
                                ParametersContext = paramsContext,
                                Body = body
                            };
                        throw;
                    }
                }
                finally
                {
                    m_blockType.RemoveAt(m_blockType.Count - 1);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_StartStatementNoSkipTokenSet);
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BlockNoSkipTokenSet);
                }

                GetNextToken();
            }
            finally
            {
                // restore state
                m_blockType = blockType;
                m_labelTable = labelTable;
            }

            return new JsFunctionObject(fncCtx, this)
                {
                    FunctionType = functionType,
                    IdContext = name.IfNotNull(n => n.Context),
                    Name = name.IfNotNull(n => n.Name),
                    ParameterDeclarations = formalParameters,
                    ParametersContext = paramsContext,
                    Body = body
                };
        }

        private void AppendImportantComments(JsBlock block)
        {
            if (block != null)
            {
                // make sure any important comments before the closing brace are kept
                if (m_importantComments.Count > 0
                    && m_settings.PreserveImportantComments
                    && m_settings.IsModificationAllowed(JsTreeModifications.PreserveImportantComments))
                {
                    // we have important comments before the EOF. Add the comment(s) to the program.
                    foreach (var importantComment in m_importantComments)
                    {
                        block.Append(new JsImportantComment(importantComment, this));
                    }

                    m_importantComments.Clear();
                }
            }
        }

        //---------------------------------------------------------------------------------------
        // ParseExpression
        //
        //  Expression :
        //    AssignmentExpressionList AssignmentExpression
        //
        //  AssignmentExpressionList :
        //    <empty> |
        //    AssignmentExpression ',' AssignmentExpressionList
        //
        //  AssignmentExpression :
        //    ConditionalExpression |
        //    LeftHandSideExpression AssignmentOperator AssignmentExpression
        //
        //  ConditionalExpression :
        //    LogicalORExpression OptionalConditionalExpression
        //
        //  OptionalConditionalExpression :
        //    <empty> |
        //    '?' AssignmentExpression ':' AssignmentExpression
        //
        //  LogicalORExpression :
        //    LogicalANDExpression OptionalLogicalOrExpression
        //
        //  OptionalLogicalOrExpression :
        //    <empty> |
        //    '||' LogicalANDExpression OptionalLogicalOrExpression
        //
        //  LogicalANDExpression :
        //    BitwiseORExpression OptionalLogicalANDExpression
        //
        //  OptionalLogicalANDExpression :
        //    <empty> |
        //    '&&' BitwiseORExpression OptionalLogicalANDExpression
        //
        //  BitwiseORExpression :
        //    BitwiseXORExpression OptionalBitwiseORExpression
        //
        //  OptionalBitwiseORExpression :
        //    <empty> |
        //    '|' BitwiseXORExpression OptionalBitwiseORExpression
        //
        //  BitwiseXORExpression :
        //    BitwiseANDExpression OptionalBitwiseXORExpression
        //
        //  OptionalBitwiseXORExpression :
        //    <empty> |
        //    '^' BitwiseANDExpression OptionalBitwiseXORExpression
        //
        //  BitwiseANDExpression :
        //    EqualityExpression OptionalBitwiseANDExpression
        //
        //  OptionalBitwiseANDExpression :
        //    <empty> |
        //    '&' EqualityExpression OptionalBitwiseANDExpression
        //
        //  EqualityExpression :
        //    RelationalExpression |
        //    RelationalExpression '==' EqualityExpression |
        //    RelationalExpression '!=' EqualityExpression |
        //    RelationalExpression '===' EqualityExpression |
        //    RelationalExpression '!==' EqualityExpression
        //
        //  RelationalExpression :
        //    ShiftExpression |
        //    ShiftExpression '<' RelationalExpression |
        //    ShiftExpression '>' RelationalExpression |
        //    ShiftExpression '<=' RelationalExpression |
        //    ShiftExpression '>=' RelationalExpression
        //
        //  ShiftExpression :
        //    AdditiveExpression |
        //    AdditiveExpression '<<' ShiftExpression |
        //    AdditiveExpression '>>' ShiftExpression |
        //    AdditiveExpression '>>>' ShiftExpression
        //
        //  AdditiveExpression :
        //    MultiplicativeExpression |
        //    MultiplicativeExpression '+' AdditiveExpression |
        //    MultiplicativeExpression '-' AdditiveExpression
        //
        //  MultiplicativeExpression :
        //    UnaryExpression |
        //    UnaryExpression '*' MultiplicativeExpression |
        //    UnaryExpression '/' MultiplicativeExpression |
        //    UnaryExpression '%' MultiplicativeExpression
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseExpression()
        {
            bool bAssign;
            JsAstNode lhs = ParseUnaryExpression(out bAssign, false);
            return ParseExpression(lhs, false, bAssign, JsToken.None);
        }

        private JsAstNode ParseExpression(bool single)
        {
            bool bAssign;
            JsAstNode lhs = ParseUnaryExpression(out bAssign, false);
            return ParseExpression(lhs, single, bAssign, JsToken.None);
        }

        private JsAstNode ParseExpression(bool single, JsToken inToken)
        {
            bool bAssign;
            JsAstNode lhs = ParseUnaryExpression(out bAssign, false);
            return ParseExpression(lhs, single, bAssign, inToken);
        }

        private JsAstNode ParseExpression(JsAstNode leftHandSide, bool single, bool bCanAssign, JsToken inToken)
        {
            // new op stack with dummy op
            Stack<JsContext> opsStack = new Stack<JsContext>();
            opsStack.Push(null);

            // term stack, push left-hand side onto it
            Stack<JsAstNode> termStack = new Stack<JsAstNode>();
            termStack.Push(leftHandSide);

            JsAstNode expr = null;

            try
            {
                for (; ; )
                {
                    // if 'binary op' or 'conditional'
                    // if we are looking for a single expression, then also bail when we hit a comma
                    // inToken is a special case because of the for..in syntax. When ParseExpression is called from
                    // for, inToken = JSToken.In which excludes JSToken.In from the list of operators, otherwise
                    // inToken = JSToken.None which is always true if the first condition is true
                    if (JsScanner.IsProcessableOperator(m_currentToken.Token)
                        && inToken != m_currentToken.Token
                        && (!single || m_currentToken.Token != JsToken.Comma))
                    {
                        // for the current token, get the operator precedence and whether it's a right-association operator
                        var prec = JsScanner.GetOperatorPrecedence(m_currentToken);
                        bool rightAssoc = JsScanner.IsRightAssociativeOperator(m_currentToken.Token);

                        // while the current operator has lower precedence than the operator at the top of the stack
                        // or it has the same precedence and it is left associative (that is, no 'assign op' or 'conditional')
                        var stackPrec = JsScanner.GetOperatorPrecedence(opsStack.Peek());
                        while (prec < stackPrec || prec == stackPrec && !rightAssoc)
                        {
                            // pop the top two elements off the stack along with the current operator, 
                            // combine them, then push the results back onto the term stack
                            JsAstNode operand2 = termStack.Pop();
                            JsAstNode operand1 = termStack.Pop();
                            expr = CreateExpressionNode(opsStack.Pop(), operand1, operand2);
                            termStack.Push(expr);

                            // get the precendence of the current item on the top of the op stack
                            stackPrec = JsScanner.GetOperatorPrecedence(opsStack.Peek());
                        }

                        // now the current operator has higher precedence that every scanned operators on the stack, or
                        // it has the same precedence as the one at the top of the stack and it is right associative
                        // push operator and next term

                        // but first: special case conditional '?:'
                        if (JsToken.ConditionalIf == m_currentToken.Token)
                        {
                            // pop term stack
                            JsAstNode condition = termStack.Pop();

                            // if this is an assignment, throw a warning in case the developer
                            // meant to use == instead of =
                            // but no warning if the condition is wrapped in parens.
                            var binOp = condition as JsBinaryOperator;
                            if (binOp != null && binOp.OperatorToken == JsToken.Assign)
                            {
                                condition.Context.HandleError(JsError.SuspectAssignment);
                            }

                            var questionCtx = m_currentToken.Clone();
                            GetNextToken();

                            // get expr1 in logOrExpr ? expr1 : expr2
                            JsAstNode operand1 = ParseExpression(true);

                            JsContext colonCtx = null;
                            if (JsToken.Colon != m_currentToken.Token)
                            {
                                ReportError(JsError.NoColon);
                            }
                            else
                            {
                                colonCtx = m_currentToken.Clone();
                            }

                            GetNextToken();

                            // get expr2 in logOrExpr ? expr1 : expr2
                            JsAstNode operand2 = ParseExpression(true, inToken);

                            expr = new JsConditional(condition.Context.CombineWith(operand2.Context), this)
                                {
                                    Condition = condition,
                                    QuestionContext = questionCtx,
                                    TrueExpression = operand1,
                                    ColonContext = colonCtx,
                                    FalseExpression = operand2
                                };
                            termStack.Push(expr);
                        }
                        else
                        {
                            if (JsScanner.IsAssignmentOperator(m_currentToken.Token))
                            {
                                if (!bCanAssign)
                                {
                                    ReportError(JsError.IllegalAssignment);
                                    SkipTokensAndThrow();
                                }
                            }
                            else
                            {
                                // if the operator is a comma, we can get another assign; otherwise we can't
                                bCanAssign = (m_currentToken.Token == JsToken.Comma);
                            }

                            // push the operator onto the operators stack
                            opsStack.Push(m_currentToken.Clone());

                            // push new term
                            GetNextToken();
                            if (bCanAssign)
                            {
                                termStack.Push(ParseUnaryExpression(out bCanAssign, false));
                            }
                            else
                            {
                                bool dummy;
                                termStack.Push(ParseUnaryExpression(out dummy, false));
                            }
                        }
                    }
                    else
                    {
                        // done with expression; go and unwind the stack of expressions/operators
                        break; 
                    }
                }

                // there are still operators to be processed
                while (opsStack.Peek() != null)
                {
                    // pop the top two term and the top operator, combine them into a new term,
                    // and push the results back onto the term stacck
                    JsAstNode operand2 = termStack.Pop();
                    JsAstNode operand1 = termStack.Pop();
                    expr = CreateExpressionNode(opsStack.Pop(), operand1, operand2);

                    // push node onto the stack
                    termStack.Push(expr);
                }

                Debug.Assert(termStack.Count == 1);
                return termStack.Pop();
            }
            catch (RecoveryTokenException exc)
            {
                exc._partiallyComputedNode = leftHandSide;
                throw;
            }
        }

        //---------------------------------------------------------------------------------------
        // ParseUnaryExpression
        //
        //  UnaryExpression :
        //    PostfixExpression |
        //    'delete' UnaryExpression |
        //    'void' UnaryExpression |
        //    'typeof' UnaryExpression |
        //    '++' UnaryExpression |
        //    '--' UnaryExpression |
        //    '+' UnaryExpression |
        //    '-' UnaryExpression |
        //    '~' UnaryExpression |
        //    '!' UnaryExpression
        //
        //---------------------------------------------------------------------------------------
        private JsAstNode ParseUnaryExpression(out bool isLeftHandSideExpr, bool isMinus)
        {
            isLeftHandSideExpr = false;
            bool dummy = false;
            JsContext exprCtx = null;
            JsAstNode expr = null;

            TryItAgain:
            JsAstNode ast = null;
            switch (m_currentToken.Token)
            {
                case JsToken.Void:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.Void
                        };
                    break;
                case JsToken.TypeOf:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.TypeOf
                        };
                    break;
                case JsToken.Plus:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.Plus
                        };
                    break;
                case JsToken.Minus:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, true);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.Minus
                        };
                    break;
                case JsToken.BitwiseNot:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.BitwiseNot
                        };
                    break;
                case JsToken.LogicalNot:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.LogicalNot
                        };
                    break;
                case JsToken.Delete:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.Delete
                        };
                    break;
                case JsToken.Increment:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.Increment
                        };
                    break;
                case JsToken.Decrement:
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    expr = ParseUnaryExpression(out dummy, false);
                    ast = new JsUnaryOperator(exprCtx.Clone().UpdateWith(expr.Context), this)
                        {
                            Operand = expr,
                            OperatorContext = exprCtx,
                            OperatorToken = JsToken.Decrement
                        };
                    break;

                case JsToken.ConditionalCommentStart:
                    // skip past the start to the next token
                    exprCtx = m_currentToken.Clone();
                    GetNextToken();
                    if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                    {
                        // empty conditional-compilation comment -- ignore
                        GetNextToken();
                        goto TryItAgain;
                    }
                    else if (m_currentToken.Token == JsToken.ConditionalCompilationOn)
                    {
                        // /*@cc_on -- check for @IDENT@*/ or !@*/
                        GetNextToken();
                        if (m_currentToken.Token == JsToken.ConditionalCompilationVariable)
                        {
                            // /*@cc_on@IDENT -- check for @*/
                            ast = new JsConstantWrapperPP(m_currentToken.Clone(), this)
                                {
                                    VarName = m_currentToken.Code,
                                    ForceComments = true
                                };

                            GetNextToken();

                            if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                            {
                                // skip the close and keep going
                                GetNextToken();
                            }
                            else
                            {
                                // too complicated
                                CCTooComplicated(null);
                                goto TryItAgain;
                            }
                        }
                        else if (m_currentToken.Token == JsToken.LogicalNot)
                        {
                            // /*@cc_on! -- check for @*/
                            var operatorContext = m_currentToken.Clone();
                            GetNextToken();
                            if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                            {
                                // we have /*@cc_on!@*/
                                GetNextToken();
                                expr = ParseUnaryExpression(out dummy, false);
                                exprCtx.UpdateWith(expr.Context);

                                var unary = new JsUnaryOperator(exprCtx, this)
                                    {
                                        Operand = expr,
                                        OperatorContext = operatorContext,
                                        OperatorToken = JsToken.LogicalNot
                                    };
                                unary.OperatorInConditionalCompilationComment = true;
                                unary.ConditionalCommentContainsOn = true;
                                ast = unary;
                            }
                            else
                            {
                                // too complicated
                                CCTooComplicated(null);
                                goto TryItAgain;
                            }
                        }
                        else
                        {
                            // too complicated
                            CCTooComplicated(null);
                            goto TryItAgain;
                        }
                    }
                    else if (m_currentToken.Token == JsToken.LogicalNot)
                    {
                        // /*@! -- check for @*/
                        var operatorContext = m_currentToken.Clone();
                        GetNextToken();
                        if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                        {
                            // we have /*@!@*/
                            GetNextToken();
                            expr = ParseUnaryExpression(out dummy, false);
                            exprCtx.UpdateWith(expr.Context);

                            var unary = new JsUnaryOperator(exprCtx, this)
                                {
                                    Operand = expr,
                                    OperatorContext = operatorContext,
                                    OperatorToken = JsToken.LogicalNot
                                };
                            unary.OperatorInConditionalCompilationComment = true;
                            ast = unary;
                        }
                        else
                        {
                            // too complicated
                            CCTooComplicated(null);
                            goto TryItAgain;
                        }
                    }
                    else if (m_currentToken.Token == JsToken.ConditionalCompilationVariable)
                    {
                        // @IDENT -- check for @*/
                        ast = new JsConstantWrapperPP(m_currentToken.Clone(), this)
                            {
                                VarName = m_currentToken.Code,
                                ForceComments = true
                            };
                        GetNextToken();

                        if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                        {
                            // skip the close and keep going
                            GetNextToken();
                        }
                        else
                        {
                            // too complicated
                            CCTooComplicated(null);
                            goto TryItAgain;
                        }
                    }
                    else
                    {
                        // we ONLY support /*@id@*/ or /*@cc_on@id@*/ or /*@!@*/ or /*@cc_on!@*/ in expressions right now. 
                        // throw an error, skip to the end of the comment, then ignore it and start
                        // looking for the next token.
                        CCTooComplicated(null);
                        goto TryItAgain;
                    }
                    break;

                default:
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_PostfixExpressionNoSkipTokenSet);
                    try
                    {
                        ast = ParseLeftHandSideExpression(isMinus);
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (IndexOfToken(NoSkipTokenSet.s_PostfixExpressionNoSkipTokenSet, exc) == -1)
                        {
                            throw;
                        }
                        else
                        {
                            if (exc._partiallyComputedNode == null)
                                SkipTokensAndThrow();
                            else
                                ast = exc._partiallyComputedNode;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_PostfixExpressionNoSkipTokenSet);
                    }
                    ast = ParsePostfixExpression(ast, out isLeftHandSideExpr);
                    break;
            }

            return ast;
        }

        private void CCTooComplicated(JsContext context)
        {
            // we ONLY support /*@id@*/ or /*@cc_on@id@*/ or /*@!@*/ or /*@cc_on!@*/ in expressions right now. 
            // throw an error, skip to the end of the comment, then ignore it and start
            // looking for the next token.
            (context ?? m_currentToken).HandleError(JsError.ConditionalCompilationTooComplex);

            // skip to end of conditional comment
            while (m_currentToken.Token != JsToken.EndOfFile && m_currentToken.Token != JsToken.ConditionalCommentEnd)
            {
                GetNextToken();
            }
            GetNextToken();
        }

        //---------------------------------------------------------------------------------------
        // ParsePostfixExpression
        //
        //  PostfixExpression:
        //    LeftHandSideExpression |
        //    LeftHandSideExpression '++' |
        //    LeftHandSideExpression  '--'
        //
        //---------------------------------------------------------------------------------------
        private JsAstNode ParsePostfixExpression(JsAstNode ast, out bool isLeftHandSideExpr)
        {
            isLeftHandSideExpr = true;
            JsContext exprCtx = null;
            if (null != ast)
            {
                if (!m_foundEndOfLine)
                {
                    if (JsToken.Increment == m_currentToken.Token)
                    {
                        isLeftHandSideExpr = false;
                        exprCtx = ast.Context.Clone();
                        exprCtx.UpdateWith(m_currentToken);
                        ast = new JsUnaryOperator(exprCtx, this)
                            {
                                Operand = ast,
                                OperatorToken = m_currentToken.Token,
                                OperatorContext = m_currentToken.Clone(),
                                IsPostfix = true
                            };
                        GetNextToken();
                    }
                    else if (JsToken.Decrement == m_currentToken.Token)
                    {
                        isLeftHandSideExpr = false;
                        exprCtx = ast.Context.Clone();
                        exprCtx.UpdateWith(m_currentToken);
                        ast = new JsUnaryOperator(exprCtx, this)
                            {
                                Operand = ast,
                                OperatorToken = m_currentToken.Token,
                                OperatorContext = m_currentToken.Clone(),
                                IsPostfix = true
                            };
                        GetNextToken();
                    }
                }
            }
            return ast;
        }

        //---------------------------------------------------------------------------------------
        // ParseLeftHandSideExpression
        //
        //  LeftHandSideExpression :
        //    PrimaryExpression Accessor  |
        //    'new' LeftHandSideExpression |
        //    FunctionExpression
        //
        //  PrimaryExpression :
        //    'this' |
        //    Identifier |
        //    Literal |
        //    '(' Expression ')'
        //
        //  FunctionExpression :
        //    'function' OptionalFuncName '(' FormalParameterList ')' { FunctionBody }
        //
        //  OptionalFuncName :
        //    <empty> |
        //    Identifier
        //---------------------------------------------------------------------------------------
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1505:AvoidUnmaintainableCode")]
        private JsAstNode ParseLeftHandSideExpression(bool isMinus)
        {
            JsAstNode ast = null;
            bool skipToken = true;
            List<JsContext> newContexts = null;

            TryItAgain:

            // new expression
            while (JsToken.New == m_currentToken.Token)
            {
                if (null == newContexts)
                    newContexts = new List<JsContext>(4);
                newContexts.Add(m_currentToken.Clone());
                GetNextToken();
            }
            JsToken token = m_currentToken.Token;
            switch (token)
            {
                // primary expression
                case JsToken.Identifier:
                    ast = new JsLookup(m_currentToken.Clone(), this)
                        {
                            Name = m_scanner.Identifier
                        };
                    break;

                case JsToken.ConditionalCommentStart:
                    // skip past the start to the next token
                    GetNextToken();
                    if (m_currentToken.Token == JsToken.ConditionalCompilationVariable)
                    {
                        // we have /*@id
                        ast = new JsConstantWrapperPP(m_currentToken.Clone(), this)
                            {
                                VarName = m_currentToken.Code,
                                ForceComments = true
                            };

                        GetNextToken();

                        if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                        {
                            // skip past the closing comment
                            GetNextToken();
                        }
                        else
                        {
                            // we ONLY support /*@id@*/ in expressions right now. If there's not
                            // a closing comment after the ID, then we don't support it.
                            // throw an error, skip to the end of the comment, then ignore it and start
                            // looking for the next token.
                            CCTooComplicated(null);
                            goto TryItAgain;
                        }
                    }
                    else if (m_currentToken.Token == JsToken.ConditionalCommentEnd)
                    {
                        // empty conditional comment! Ignore.
                        GetNextToken();
                        goto TryItAgain;
                    }
                    else
                    {
                        // we DON'T have "/*@IDENT". We only support "/*@IDENT @*/", so since this isn't
                        // and id, throw the error, skip to the end of the comment, and ignore it
                        // by looping back and looking for the NEXT token.
                        m_currentToken.HandleError(JsError.ConditionalCompilationTooComplex);

                        // skip to end of conditional comment
                        while (m_currentToken.Token != JsToken.EndOfFile && m_currentToken.Token != JsToken.ConditionalCommentEnd)
                        {
                            GetNextToken();
                        }
                        GetNextToken();
                        goto TryItAgain;
                    }
                    break;

                case JsToken.This:
                    ast = new JsThisLiteral(m_currentToken.Clone(), this);
                    break;

                case JsToken.StringLiteral:
                    ast = new JsConstantWrapper(m_scanner.StringLiteralValue, JsPrimitiveType.String, m_currentToken.Clone(), this)
                        {
                            MayHaveIssues = m_scanner.LiteralHasIssues
                        };
                    break;

                case JsToken.IntegerLiteral:
                case JsToken.NumericLiteral:
                    {
                        JsContext numericContext = m_currentToken.Clone();
                        double doubleValue;
                        if (ConvertNumericLiteralToDouble(m_currentToken.Code, (token == JsToken.IntegerLiteral), out doubleValue))
                        {
                            // conversion worked fine
                            // check for some boundary conditions
                            var mayHaveIssues = m_scanner.LiteralHasIssues;
                            if (doubleValue == double.MaxValue)
                            {
                                ReportError(JsError.NumericMaximum, numericContext, true);
                            }
                            else if (isMinus && -doubleValue == double.MinValue)
                            {
                                ReportError(JsError.NumericMinimum, numericContext, true);
                            }

                            // create the constant wrapper from the value
                            ast = new JsConstantWrapper(doubleValue, JsPrimitiveType.Number, numericContext, this)
                                {
                                    MayHaveIssues = mayHaveIssues
                                };
                        }
                        else
                        {
                            // if we went overflow or are not a number, then we will use the "Other"
                            // primitive type so we don't try doing any numeric calcs with it. 
                            if (double.IsInfinity(doubleValue))
                            {
                                // overflow
                                // and if we ARE an overflow, report it
                                ReportError(JsError.NumericOverflow, numericContext, true);
                            }

                            // regardless, we're going to create a special constant wrapper
                            // that simply echos the input as-is
                            ast = new JsConstantWrapper(m_currentToken.Code, JsPrimitiveType.Other, numericContext, this)
                            {
                                MayHaveIssues = true
                            };
                        }
                        break;
                    }

                case JsToken.True:
                    ast = new JsConstantWrapper(true, JsPrimitiveType.Boolean, m_currentToken.Clone(), this);
                    break;

                case JsToken.False:
                    ast = new JsConstantWrapper(false, JsPrimitiveType.Boolean, m_currentToken.Clone(), this);
                    break;

                case JsToken.Null:
                    ast = new JsConstantWrapper(null, JsPrimitiveType.Null, m_currentToken.Clone(), this);
                    break;

                case JsToken.ConditionalCompilationVariable:
                    ast = new JsConstantWrapperPP(m_currentToken.Clone(), this)
                        {
                            VarName = m_currentToken.Code,
                            ForceComments = false
                        };
                    break;

                case JsToken.DivideAssign:
                // normally this token is not allowed on the left-hand side of an expression.
                // BUT, this might be the start of a regular expression that begins with an equals sign!
                // we need to test to see if we can parse a regular expression, and if not, THEN
                // we can fail the parse.

                case JsToken.Divide:
                    // could it be a regexp?
                    ast = ScanRegularExpression();
                    if (ast != null)
                    {
                        // yup -- we're done here
                        break;
                    }

                    // nope -- go to the default branch
                    goto default;

                // expression
                case JsToken.LeftParenthesis:
                    {
                        var groupingOp = new JsGroupingOperator(m_currentToken.Clone(), this);
                        ast = groupingOp;
                        GetNextToken();
                        m_noSkipTokenSet.Add(NoSkipTokenSet.s_ParenExpressionNoSkipToken);
                        try
                        {
                            // parse an expression
                            groupingOp.Operand = ParseExpression();
                            if (JsToken.RightParenthesis != m_currentToken.Token)
                            {
                                ReportError(JsError.NoRightParenthesis);
                            }
                            else
                            {
                                // add the closing paren to the expression context
                                ast.Context.UpdateWith(m_currentToken);
                            }
                        }
                        catch (RecoveryTokenException exc)
                        {
                            if (IndexOfToken(NoSkipTokenSet.s_ParenExpressionNoSkipToken, exc) == -1)
                                throw;
                            else
                                groupingOp.Operand = exc._partiallyComputedNode;
                        }
                        finally
                        {
                            m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ParenExpressionNoSkipToken);
                        }
                    }
                    break;

                // array initializer
                case JsToken.LeftBracket:
                    JsContext listCtx = m_currentToken.Clone();
                    GetNextToken();
                    JsAstNodeList list = new JsAstNodeList(CurrentPositionContext(), this);
                    var hasTrailingCommas = false;
                    while (JsToken.RightBracket != m_currentToken.Token)
                    {
                        if (JsToken.Comma != m_currentToken.Token)
                        {
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_ArrayInitNoSkipTokenSet);
                            try
                            {
                                var expression = ParseExpression(true);
                                list.Append(expression);
                                if (JsToken.Comma != m_currentToken.Token)
                                {
                                    if (JsToken.RightBracket != m_currentToken.Token)
                                    {
                                        ReportError(JsError.NoRightBracket);
                                    }

                                    break;
                                }
                                else
                                {
                                    // we have a comma -- skip it after adding it as a terminator
                                    // on the previous expression
                                    var commaContext = m_currentToken.Clone();
                                    expression.IfNotNull(e => e.TerminatingContext = commaContext);
                                    GetNextToken();

                                    // if the next token is the closing brackets, then we need to
                                    // add a missing value to the array because we end in a comma and
                                    // we need to keep it for cross-platform compat.
                                    // TECHNICALLY, that puts an extra item into the array for most modern browsers, but not ALL.
                                    if (m_currentToken.Token == JsToken.RightBracket)
                                    {
                                        hasTrailingCommas = true;
                                        list.Append(new JsConstantWrapper(JsMissing.Value, JsPrimitiveType.Other, m_currentToken.Clone(), this));

                                        // throw a cross-browser warning about trailing commas
                                        commaContext.HandleError(JsError.ArrayLiteralTrailingComma);
                                        break;
                                    }
                                }
                            }
                            catch (RecoveryTokenException exc)
                            {
                                if (exc._partiallyComputedNode != null)
                                    list.Append(exc._partiallyComputedNode);
                                if (IndexOfToken(NoSkipTokenSet.s_ArrayInitNoSkipTokenSet, exc) == -1)
                                {
                                    listCtx.UpdateWith(CurrentPositionContext());
                                    exc._partiallyComputedNode = new JsArrayLiteral(listCtx, this)
                                        {
                                            Elements = list,
                                            MayHaveIssues = true
                                        };
                                    throw;
                                }
                                else
                                {
                                    if (JsToken.RightBracket == m_currentToken.Token)
                                        break;
                                }
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ArrayInitNoSkipTokenSet);
                            }
                        }
                        else
                        {
                            // comma -- missing array item in the list
                            var commaContext = m_currentToken.Clone();
                            list.Append(new JsConstantWrapper(JsMissing.Value, JsPrimitiveType.Other, m_currentToken.Clone(), this)
                                {
                                    TerminatingContext = commaContext
                                });

                            // skip over the comma
                            GetNextToken();

                            // if the next token is the closing brace, then we end with a comma -- and we need to
                            // add ANOTHER missing value to make sure this last comma doesn't get left off.
                            // TECHNICALLY, that puts an extra item into the array for most modern browsers, but not ALL.
                            if (m_currentToken.Token == JsToken.RightBracket)
                            {
                                hasTrailingCommas = true;
                                list.Append(new JsConstantWrapper(JsMissing.Value, JsPrimitiveType.Other, m_currentToken.Clone(), this));

                                // throw a cross-browser warning about trailing commas
                                commaContext.HandleError(JsError.ArrayLiteralTrailingComma);
                                break;
                            }
                        }
                    }

                    listCtx.UpdateWith(m_currentToken);
                    ast = new JsArrayLiteral(listCtx, this)
                        {
                            Elements = list,
                            MayHaveIssues = hasTrailingCommas
                        };
                    break;

                // object initializer
                case JsToken.LeftCurly:
                    JsContext objCtx = m_currentToken.Clone();
                    GetNextToken();

                    var propertyList = new JsAstNodeList(CurrentPositionContext(), this);

                    if (JsToken.RightCurly != m_currentToken.Token)
                    {
                        for (; ; )
                        {
                            JsObjectLiteralField field = null;
                            JsAstNode value = null;
                            bool getterSetter = false;
                            string ident;

                            switch (m_currentToken.Token)
                            {
                                case JsToken.Identifier:
                                    field = new JsObjectLiteralField(m_scanner.Identifier, JsPrimitiveType.String, m_currentToken.Clone(), this);
                                    break;

                                case JsToken.StringLiteral:
                                    field = new JsObjectLiteralField(m_scanner.StringLiteralValue, JsPrimitiveType.String, m_currentToken.Clone(), this)
                                        {
                                            MayHaveIssues = m_scanner.LiteralHasIssues
                                        };
                                    break;

                                case JsToken.IntegerLiteral:
                                case JsToken.NumericLiteral:
                                    {
                                        double doubleValue;
                                        if (ConvertNumericLiteralToDouble(m_currentToken.Code, (m_currentToken.Token == JsToken.IntegerLiteral), out doubleValue))
                                        {
                                            // conversion worked fine
                                            field = new JsObjectLiteralField(
                                              doubleValue,
                                              JsPrimitiveType.Number,
                                              m_currentToken.Clone(),
                                              this
                                              );
                                        }
                                        else
                                        {
                                            // something went wrong and we're not sure the string representation in the source is 
                                            // going to convert to a numeric value well
                                            if (double.IsInfinity(doubleValue))
                                            {
                                                ReportError(JsError.NumericOverflow, m_currentToken.Clone(), true);
                                            }

                                            // use the source as the field name, not the numeric value
                                            field = new JsObjectLiteralField(
                                                m_currentToken.Code,
                                                JsPrimitiveType.Other,
                                                m_currentToken.Clone(),
                                                this);
                                        }
                                        break;
                                    }

                                case JsToken.Get:
                                case JsToken.Set:
                                    if (PeekToken() == JsToken.Colon)
                                    {
                                        // the field is either "get" or "set" and isn't the special Mozilla getter/setter
                                        field = new JsObjectLiteralField(m_currentToken.Code, JsPrimitiveType.String, m_currentToken.Clone(), this);
                                    }
                                    else
                                    {
                                        // ecma-script get/set property construct
                                        getterSetter = true;
                                        bool isGet = (m_currentToken.Token == JsToken.Get);
                                        value = ParseFunction(
                                          (JsToken.Get == m_currentToken.Token ? JsFunctionType.Getter : JsFunctionType.Setter),
                                          m_currentToken.Clone()
                                          );
                                        JsFunctionObject funcExpr = value as JsFunctionObject;
                                        if (funcExpr != null)
                                        {
                                            // getter/setter is just the literal name with a get/set flag
                                            field = new JsGetterSetter(
                                              funcExpr.Name,
                                              isGet,
                                              funcExpr.IdContext.Clone(),
                                              this
                                              );
                                        }
                                        else
                                        {
                                            ReportError(JsError.FunctionExpressionExpected);
                                        }
                                    }
                                    break;

                                default:
                                    // NOT: identifier token, string, number, or getter/setter.
                                    // see if it's a token that COULD be an identifierName.
                                    ident = m_scanner.Identifier;
                                    if (JsScanner.IsValidIdentifier(ident))
                                    {
                                        // BY THE SPEC, if it's a valid identifierName -- which includes reserved words -- then it's
                                        // okay for object literal syntax. However, reserved words here won't work in all browsers,
                                        // so if it is a reserved word, let's throw a low-sev cross-browser warning on the code.
                                        if (JsKeyword.CanBeIdentifier(m_currentToken.Token) == null)
                                        {
                                            ReportError(JsError.ObjectLiteralKeyword, m_currentToken.Clone(), true);
                                        }

                                        field = new JsObjectLiteralField(ident, JsPrimitiveType.String, m_currentToken.Clone(), this);
                                    }
                                    else
                                    {
                                        // throw an error but use it anyway, since that's what the developer has going on
                                        ReportError(JsError.NoMemberIdentifier, m_currentToken.Clone(), true);
                                        field = new JsObjectLiteralField(m_currentToken.Code, JsPrimitiveType.String, m_currentToken.Clone(), this);
                                    }
                                    break;
                            }

                            if (field != null)
                            {
                                if (!getterSetter)
                                {
                                    GetNextToken();
                                }

                                m_noSkipTokenSet.Add(NoSkipTokenSet.s_ObjectInitNoSkipTokenSet);
                                try
                                {
                                    if (!getterSetter)
                                    {
                                        // get the value
                                        if (JsToken.Colon != m_currentToken.Token)
                                        {
                                            ReportError(JsError.NoColon, true);
                                            value = ParseExpression(true);
                                        }
                                        else
                                        {
                                            field.ColonContext = m_currentToken.Clone();
                                            GetNextToken();
                                            value = ParseExpression(true);
                                        }
                                    }

                                    // put the pair into the list of fields
                                    var propCtx = field.Context.Clone().CombineWith(value.IfNotNull(v => v.Context));
                                    var property = new JsObjectLiteralProperty(propCtx, this)
                                        {
                                            Name = field,
                                            Value = value
                                        };

                                    propertyList.Append(property);

                                    if (JsToken.RightCurly == m_currentToken.Token)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        if (JsToken.Comma == m_currentToken.Token)
                                        {
                                            // skip the comma after adding it to the property as a terminating context
                                            property.IfNotNull(p => p.TerminatingContext = m_currentToken.Clone());
                                            GetNextToken();

                                            // if the next token is the right-curly brace, then we ended 
                                            // the list with a comma, which is perfectly fine
                                            if (m_currentToken.Token == JsToken.RightCurly)
                                            {
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            if (m_foundEndOfLine)
                                            {
                                                ReportError(JsError.NoRightCurly);
                                            }
                                            else
                                                ReportError(JsError.NoComma, true);
                                            SkipTokensAndThrow();
                                        }
                                    }
                                }
                                catch (RecoveryTokenException exc)
                                {
                                    if (exc._partiallyComputedNode != null)
                                    {
                                        // the problem was in ParseExpression trying to determine value
                                        value = exc._partiallyComputedNode;

                                        var propCtx = field.Context.Clone().CombineWith(value.IfNotNull(v => v.Context));
                                        var property = new JsObjectLiteralProperty(propCtx, this)
                                        {
                                            Name = field,
                                            Value = value
                                        };

                                        propertyList.Append(property);
                                    }

                                    if (IndexOfToken(NoSkipTokenSet.s_ObjectInitNoSkipTokenSet, exc) == -1)
                                    {
                                        exc._partiallyComputedNode = new JsObjectLiteral(objCtx, this)
                                            {
                                                Properties = propertyList
                                            };
                                        throw;
                                    }
                                    else
                                    {
                                        if (JsToken.Comma == m_currentToken.Token)
                                            GetNextToken();
                                        if (JsToken.RightCurly == m_currentToken.Token)
                                            break;
                                    }
                                }
                                finally
                                {
                                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ObjectInitNoSkipTokenSet);
                                }
                            }
                        }
                    }
                    objCtx.UpdateWith(m_currentToken);
                    ast = new JsObjectLiteral(objCtx, this)
                        {
                            Properties = propertyList
                        };
                    break;

                // function expression
                case JsToken.Function:
                    ast = ParseFunction(JsFunctionType.Expression, m_currentToken.Clone());
                    skipToken = false;
                    break;

                case JsToken.AspNetBlock:
                    ast = new JsAspNetBlockNode(m_currentToken.Clone(), this)
                        {
                            AspNetBlockText = m_currentToken.Code
                        };
                    break;

                default:
                    string identifier = JsKeyword.CanBeIdentifier(m_currentToken.Token);
                    if (null != identifier)
                    {
                        ast = new JsLookup(m_currentToken.Clone(), this)
                            {
                                Name = identifier
                            };
                    }
                    else
                    {
                        ReportError(JsError.ExpressionExpected);
                        SkipTokensAndThrow();
                    }
                    break;
            }

            // can be a CallExpression, that is, followed by '.' or '(' or '['
            if (skipToken)
                GetNextToken();

            return MemberExpression(ast, newContexts);
        }

        /// <summary>
        /// Convert the given numeric string to a double value
        /// </summary>
        /// <param name="str">string representation of a number</param>
        /// <param name="isInteger">we should know alreasdy if it's an integer or not</param>
        /// <param name="doubleValue">output value</param>
        /// <returns>true if there were no problems; false if there were</returns>
        private bool ConvertNumericLiteralToDouble(string str, bool isInteger, out double doubleValue)
        {
            try
            {
                if (isInteger)
                {
                    if (str[0] == '0' && str.Length > 1)
                    {
                        if (str[1] == 'x' || str[1] == 'X')
                        {
                            if (str.Length == 2)
                            {
                                // 0x???? must be a parse error. Just return zero
                                doubleValue = 0;
                                return false;
                            }

                            // parse the number as a hex integer, converted to a double
                            doubleValue = (double)System.Convert.ToInt64(str, 16);
                        }
                        else if (str[1] == 'o' || str[1] == 'O')
                        {
                            if (str.Length == 2)
                            {
                                // 0o???? must be a parse error. Just return zero
                                doubleValue = 0;
                                return false;
                            }

                            // parse the number as an octal integer without the prefix, converted to a double
                            doubleValue = (double)System.Convert.ToInt64(str.Substring(2), 8);
                        }
                        else if (str[1] == 'b' || str[1] == 'B')
                        {
                            if (str.Length == 2)
                            {
                                // 0b???? must be a parse error. Just return zero
                                doubleValue = 0;
                                return false;
                            }

                            // parse the number as a binary integer without the prefix, converted to a double
                            doubleValue = (double)System.Convert.ToInt64(str.Substring(2), 2);
                        }
                        else
                        {
                            // might be an octal value... try converting to octal
                            // and if it fails, just convert to decimal
                            try
                            {
                                doubleValue = (double)System.Convert.ToInt64(str, 8);

                                // if we got here, we successfully converted it to octal.
                                // now, octal literals are deprecated -- not all JS implementations will
                                // decode them. If this decoded as an octal, it can also be a decimal. Check
                                // the decimal value, and if it's the same, then we'll just treat it
                                // as a normal decimal value. Otherwise we'll throw a warning and treat it
                                // as a special no-convert literal.
                                double decimalValue = (double)System.Convert.ToInt64(str, 10);
                                if (decimalValue != doubleValue)
                                {
                                    // throw a warning!
                                    ReportError(JsError.OctalLiteralsDeprecated, m_currentToken.Clone(), true);

                                    // return false because octals are deprecated and might have
                                    // cross-browser issues
                                    return false;
                                }
                            }
                            catch (FormatException)
                            {
                                // ignore the format exception and fall through to parsing
                                // the value as a base-10 decimal value
                                doubleValue = Convert.ToDouble(str, CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    else
                    {
                        // just parse the integer as a decimal value
                        doubleValue = Convert.ToDouble(str, CultureInfo.InvariantCulture);
                    }

                    // check for out-of-bounds integer values -- if the integer can't EXACTLY be represented
                    // as a double, then we don't want to consider it "successful"
                    if (doubleValue < -0x20000000000000 || 0x20000000000000 < doubleValue)
                    {
                        return false;
                    }
                }
                else
                {
                    // use the system to convert the string to a double
                    doubleValue = Convert.ToDouble(str, CultureInfo.InvariantCulture);
                }

                // if we got here, we should have an appropriate value in doubleValue
                return true;
            }
            catch (OverflowException)
            {
                // overflow mean just return one of the infinity values
                doubleValue = (str[0] == '-'
                  ? Double.NegativeInfinity
                  : Double.PositiveInfinity
                  );

                // and it wasn't "successful"
                return false;
            }
            catch (FormatException)
            {
                // format exception converts to NaN
                doubleValue = double.NaN;

                // not successful
                return false;
            }
        }

        //---------------------------------------------------------------------------------------
        // MemberExpression
        //
        // Accessor :
        //  <empty> |
        //  Arguments Accessor
        //  '[' Expression ']' Accessor |
        //  '.' Identifier Accessor |
        //
        //  Don't have this function throwing an exception without checking all the calling sites.
        //  There is state in instance variable that is saved on the calling stack in some function
        //  (i.e ParseFunction and ParseClass) and you don't want to blow up the stack
        //---------------------------------------------------------------------------------------
        private JsAstNode MemberExpression(JsAstNode expression, List<JsContext> newContexts)
        {
            for (; ; )
            {
                m_noSkipTokenSet.Add(NoSkipTokenSet.s_MemberExprNoSkipTokenSet);
                try
                {
                    switch (m_currentToken.Token)
                    {
                        case JsToken.LeftParenthesis:
                            JsAstNodeList args = null;
                            RecoveryTokenException callError = null;
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_ParenToken);
                            try
                            {
                                args = ParseExpressionList(JsToken.RightParenthesis);
                            }
                            catch (RecoveryTokenException exc)
                            {
                                args = (JsAstNodeList)exc._partiallyComputedNode;
                                if (IndexOfToken(NoSkipTokenSet.s_ParenToken, exc) == -1)
                                    callError = exc; // thrown later on
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ParenToken);
                            }

                            expression = new JsCallNode(expression.Context.CombineWith(args.Context), this)
                                {
                                    Function = expression,
                                    Arguments = args,
                                    InBrackets = false
                                };

                            if (null != newContexts && newContexts.Count > 0)
                            {
                                (newContexts[newContexts.Count - 1]).UpdateWith(expression.Context);
                                if (!(expression is JsCallNode))
                                {
                                    expression = new JsCallNode(newContexts[newContexts.Count - 1], this)
                                        {
                                            Function = expression,
                                            Arguments = new JsAstNodeList(CurrentPositionContext(), this)
                                        };
                                }
                                else
                                {
                                    expression.Context = newContexts[newContexts.Count - 1];
                                }

                                ((JsCallNode)expression).IsConstructor = true;
                                newContexts.RemoveAt(newContexts.Count - 1);
                            }

                            if (callError != null)
                            {
                                callError._partiallyComputedNode = expression;
                                throw callError;
                            }

                            GetNextToken();
                            break;

                        case JsToken.LeftBracket:
                            m_noSkipTokenSet.Add(NoSkipTokenSet.s_BracketToken);
                            try
                            {
                                //
                                // ROTOR parses a[b,c] as a call to a, passing in the arguments b and c.
                                // the correct parse is a member lookup on a of c -- the "b,c" should be
                                // a single expression with a comma operator that evaluates b but only
                                // returns c.
                                // So we'll change the default behavior from parsing an expression list to
                                // parsing a single expression, but returning a single-item list (or an empty
                                // list if there is no expression) so the rest of the code will work.
                                //
                                //args = ParseExpressionList(JSToken.RightBracket);
                                GetNextToken();
                                args = new JsAstNodeList(CurrentPositionContext(), this);

                                JsAstNode accessor = ParseExpression();
                                if (accessor != null)
                                {
                                    args.Append(accessor);
                                }
                            }
                            catch (RecoveryTokenException exc)
                            {
                                if (IndexOfToken(NoSkipTokenSet.s_BracketToken, exc) == -1)
                                {
                                    if (exc._partiallyComputedNode != null)
                                    {
                                        exc._partiallyComputedNode =
                                           new JsCallNode(expression.Context.CombineWith(m_currentToken.Clone()), this)
                                            {
                                                Function = expression,
                                                Arguments = (JsAstNodeList)exc._partiallyComputedNode,
                                                InBrackets = true
                                            };
                                    }
                                    else
                                    {
                                        exc._partiallyComputedNode = expression;
                                    }
                                    throw;
                                }
                                else
                                    args = (JsAstNodeList)exc._partiallyComputedNode;
                            }
                            finally
                            {
                                m_noSkipTokenSet.Remove(NoSkipTokenSet.s_BracketToken);
                            }
                            expression = new JsCallNode(expression.Context.CombineWith(m_currentToken.Clone()), this)
                                {
                                    Function = expression,
                                    Arguments = args,
                                    InBrackets = true
                                };

                            // there originally was code here in the ROTOR sources that checked the new context list and
                            // changed this member call to a constructor call, effectively combining the two. I believe they
                            // need to remain separate.

                            // remove the close bracket token
                            GetNextToken();
                            break;

                        case JsToken.AccessField:
                            JsConstantWrapper id = null;
                            JsContext nameContext = m_currentToken.Clone();
                            GetNextToken();
                            if (JsToken.Identifier != m_currentToken.Token)
                            {
                                string identifier = JsKeyword.CanBeIdentifier(m_currentToken.Token);
                                if (null != identifier)
                                {
                                    // don't report an error here -- it's actually okay to have a property name
                                    // that is a keyword which is okay to be an identifier. For instance,
                                    // jQuery has a commonly-used method named "get" to make an ajax request
                                    //ForceReportInfo(JSError.KeywordUsedAsIdentifier);
                                    id = new JsConstantWrapper(identifier, JsPrimitiveType.String, m_currentToken.Clone(), this);
                                }
                                else if (JsScanner.IsValidIdentifier(m_currentToken.Code))
                                {
                                    // it must be a keyword, because it can't technically be an identifier,
                                    // but it IS a valid identifier format. Throw a warning but still
                                    // create the constant wrapper so we can output it as-is
                                    ReportError(JsError.KeywordUsedAsIdentifier, m_currentToken.Clone(), true);
                                    id = new JsConstantWrapper(m_currentToken.Code, JsPrimitiveType.String, m_currentToken.Clone(), this);
                                }
                                else
                                {
                                    ReportError(JsError.NoIdentifier);
                                    SkipTokensAndThrow(expression);
                                }
                            }
                            else
                            {
                                id = new JsConstantWrapper(m_scanner.Identifier, JsPrimitiveType.String, m_currentToken.Clone(), this);
                            }
                            GetNextToken();
                            expression = new JsMember(expression.Context.CombineWith(id.Context), this)
                                {
                                    Root = expression,
                                    Name = id.Context.Code,
                                    NameContext = nameContext.CombineWith(id.Context)
                                };
                            break;
                        default:
                            if (null != newContexts)
                            {
                                while (newContexts.Count > 0)
                                {
                                    (newContexts[newContexts.Count - 1]).UpdateWith(expression.Context);
                                    expression = new JsCallNode(newContexts[newContexts.Count - 1], this)
                                        {
                                            Function = expression,
                                            Arguments = new JsAstNodeList(CurrentPositionContext(), this)
                                        };
                                    ((JsCallNode)expression).IsConstructor = true;
                                    newContexts.RemoveAt(newContexts.Count - 1);
                                }
                            }
                            return expression;
                    }
                }
                catch (RecoveryTokenException exc)
                {
                    if (IndexOfToken(NoSkipTokenSet.s_MemberExprNoSkipTokenSet, exc) != -1)
                        expression = exc._partiallyComputedNode;
                    else
                    {
                        Debug.Assert(exc._partiallyComputedNode == expression);
                        throw;
                    }
                }
                finally
                {
                    m_noSkipTokenSet.Remove(NoSkipTokenSet.s_MemberExprNoSkipTokenSet);
                }
            }
        }

        //---------------------------------------------------------------------------------------
        // ParseExpressionList
        //
        //  Given a starting this.currentToken '(' or '[', parse a list of expression separated by
        //  ',' until matching ')' or ']'
        //---------------------------------------------------------------------------------------
        private JsAstNodeList ParseExpressionList(JsToken terminator)
        {
            JsContext listCtx = m_currentToken.Clone();
            GetNextToken();
            JsAstNodeList list = new JsAstNodeList(listCtx, this);
            if (terminator != m_currentToken.Token)
            {
                for (; ; )
                {
                    m_noSkipTokenSet.Add(NoSkipTokenSet.s_ExpressionListNoSkipTokenSet);
                    try
                    {
                        JsAstNode item;
                        if (JsToken.Comma == m_currentToken.Token)
                        {
                            item = new JsConstantWrapper(JsMissing.Value, JsPrimitiveType.Other, m_currentToken.Clone(), this);
                            list.Append(item);
                        }
                        else if (terminator == m_currentToken.Token)
                        {
                            break;
                        }
                        else
                        {
                            item = ParseExpression(true);
                            list.Append(item);
                        }

                        if (terminator == m_currentToken.Token)
                        {
                            break;
                        }
                        else
                        {
                            if (JsToken.Comma == m_currentToken.Token)
                            {
                                item.IfNotNull(n => n.TerminatingContext = m_currentToken.Clone());
                            }
                            else
                            {
                                if (terminator == JsToken.RightParenthesis)
                                {
                                    //  in ASP+ it's easy to write a semicolon at the end of an expression
                                    //  not realizing it is going to go inside a function call
                                    //  (ie. Response.Write()), so make a special check here
                                    if (JsToken.Semicolon == m_currentToken.Token)
                                    {
                                        if (JsToken.RightParenthesis == PeekToken())
                                        {
                                            ReportError(JsError.UnexpectedSemicolon, true);
                                            GetNextToken();
                                            break;
                                        }
                                    }

                                    ReportError(JsError.NoRightParenthesisOrComma);
                                }
                                else
                                {
                                    ReportError(JsError.NoRightBracketOrComma);
                                }

                                SkipTokensAndThrow();
                            }
                        }
                    }
                    catch (RecoveryTokenException exc)
                    {
                        if (exc._partiallyComputedNode != null)
                            list.Append(exc._partiallyComputedNode);
                        if (IndexOfToken(NoSkipTokenSet.s_ExpressionListNoSkipTokenSet, exc) == -1)
                        {
                            exc._partiallyComputedNode = list;
                            throw;
                        }
                    }
                    finally
                    {
                        m_noSkipTokenSet.Remove(NoSkipTokenSet.s_ExpressionListNoSkipTokenSet);
                    }
                    GetNextToken();
                }
            }
            listCtx.UpdateWith(m_currentToken);
            return list;
        }

        //---------------------------------------------------------------------------------------
        // CreateExpressionNode
        //
        //  Create the proper AST object according to operator
        //---------------------------------------------------------------------------------------
        private JsAstNode CreateExpressionNode(JsContext op, JsAstNode operand1, JsAstNode operand2)
        {
            JsContext context = operand1.Context.CombineWith(operand2.Context);
            switch (op.Token)
            {
                case JsToken.Assign:
                case JsToken.BitwiseAnd:
                case JsToken.BitwiseAndAssign:
                case JsToken.BitwiseOr:
                case JsToken.BitwiseOrAssign:
                case JsToken.BitwiseXor:
                case JsToken.BitwiseXorAssign:
                case JsToken.Divide:
                case JsToken.DivideAssign:
                case JsToken.Equal:
                case JsToken.GreaterThan:
                case JsToken.GreaterThanEqual:
                case JsToken.In:
                case JsToken.InstanceOf:
                case JsToken.LeftShift:
                case JsToken.LeftShiftAssign:
                case JsToken.LessThan:
                case JsToken.LessThanEqual:
                case JsToken.LogicalAnd:
                case JsToken.LogicalOr:
                case JsToken.Minus:
                case JsToken.MinusAssign:
                case JsToken.Modulo:
                case JsToken.ModuloAssign:
                case JsToken.Multiply:
                case JsToken.MultiplyAssign:
                case JsToken.NotEqual:
                case JsToken.Plus:
                case JsToken.PlusAssign:
                case JsToken.RightShift:
                case JsToken.RightShiftAssign:
                case JsToken.StrictEqual:
                case JsToken.StrictNotEqual:
                case JsToken.UnsignedRightShift:
                case JsToken.UnsignedRightShiftAssign:
                    // regular binary operator
                    return new JsBinaryOperator(context, this)
                        {
                            Operand1 = operand1,
                            Operand2 = operand2,
                            OperatorContext = op,
                            OperatorToken = op.Token
                        };

                case JsToken.Comma:
                    // use the special comma-operator class derived from binary operator.
                    // it has special logic to combine adjacent comma operators into a single
                    // node with an ast node list rather than nested binary operators
                    return JsCommaOperator.CombineWithComma(context, this, operand1, operand2);

                default:
                    // shouldn't get here!
                    Debug.Assert(false);
                    return null;
            }
        }

        //---------------------------------------------------------------------------------------
        // GetNextToken
        //
        //  Return the next token or peeked token if this.errorToken is not null.
        //  Usually this.errorToken is set by AddError even though any code can look ahead
        //  by assigning this.errorToken.
        //  At this point the context is not saved so if position information is needed
        //  they have to be saved explicitely
        //---------------------------------------------------------------------------------------
        private void GetNextToken()
        {
            if (m_useCurrentForNext)
            {
                // we just want to keep using the current token.
                // but don't get into an infinite loop -- after a while,
                // give up and grab the next token from the scanner anyway
                m_useCurrentForNext = false;
                if (m_breakRecursion++ > 10)
                {
                    m_currentToken = ScanNextToken();
                }
            }
            else
            {
                m_goodTokensProcessed++;
                m_breakRecursion = 0;

                // the scanner reuses the same context object for performance,
                // so if we ever mean to hold onto it later, we need to clone it.
                m_currentToken = ScanNextToken();
            }
        }

        private JsAstNode ScanRegularExpression()
        {
            var source = m_scanner.ScanRegExp();
            if (source != null)
            {
                // parse the flags (if any)
                var flags = m_scanner.ScanRegExpFlags();

                // create the regexp node and return it 
                return new JsRegExpLiteral(m_currentToken.Clone(), this)
                {
                    Pattern = source,
                    PatternSwitches = flags
                };
            }

            // if we get here, there isn't a regular expression at the current position
            return null;
        }

        private JsContext ScanNextToken()
        {
            EchoWriter.IfNotNull(w => { if (m_currentToken.Token != JsToken.None) w.Write(m_currentToken.Code); });

            m_newModule = false;
            m_foundEndOfLine = false;
            m_importantComments.Clear();

            var nextToken = m_scanner.ScanNextToken(false);
            while (nextToken.Token == JsToken.WhiteSpace
                || nextToken.Token == JsToken.EndOfLine
                || nextToken.Token == JsToken.SingleLineComment
                || nextToken.Token == JsToken.MultipleLineComment
                || nextToken.Token == JsToken.Error
                || nextToken.Token == JsToken.PreprocessorDirective)
            {
                if (nextToken.Token == JsToken.EndOfLine)
                {
                    m_foundEndOfLine = true;
                }
                else if (nextToken.Token == JsToken.MultipleLineComment || nextToken.Token == JsToken.SingleLineComment)
                {
                    if (nextToken.HasCode 
                        && ((nextToken.Code.Length > 2 && nextToken.Code[2] == '!') 
                        || (nextToken.Code.IndexOf("@preserve", StringComparison.OrdinalIgnoreCase) >= 0)
                        || (nextToken.Code.IndexOf("@license", StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        // this is an important comment -- save it for later
                        m_importantComments.Add(nextToken.Clone());
                    }
                }

                // if we are preprocess-only, then don't output any preprocessor directive tokens
                EchoWriter.IfNotNull(w => { if (!Settings.PreprocessOnly || nextToken.Token != JsToken.PreprocessorDirective) w.Write(nextToken.Code); });
                nextToken = m_scanner.ScanNextToken(false);
            }

            if (nextToken.Token == JsToken.EndOfFile)
            {
                m_foundEndOfLine = true;
            }

            return nextToken;
        }

        private JsToken PeekToken()
        {
            // clone the scanner, turn off any error reporting, and get the next token
            var clonedScanner = m_scanner.Clone();
            clonedScanner.SuppressErrors = true;
            var peekToken = clonedScanner.ScanNextToken(false);

            // there are some tokens we really don't care about when we peek
            // for the next token
            while (peekToken.Token == JsToken.WhiteSpace
                || peekToken.Token == JsToken.EndOfLine
                || peekToken.Token == JsToken.Error
                || peekToken.Token == JsToken.SingleLineComment
                || peekToken.Token == JsToken.MultipleLineComment
                || peekToken.Token == JsToken.PreprocessorDirective
                || peekToken.Token == JsToken.ConditionalCommentEnd
                || peekToken.Token == JsToken.ConditionalCommentStart
                || peekToken.Token == JsToken.ConditionalCompilationElse
                || peekToken.Token == JsToken.ConditionalCompilationElseIf
                || peekToken.Token == JsToken.ConditionalCompilationEnd
                || peekToken.Token == JsToken.ConditionalCompilationIf
                || peekToken.Token == JsToken.ConditionalCompilationOn
                || peekToken.Token == JsToken.ConditionalCompilationSet
                || peekToken.Token == JsToken.ConditionalCompilationVariable
                || peekToken.Token == JsToken.ConditionalIf)
            {
                peekToken = clonedScanner.ScanNextToken(false);
            }

            // return the token type
            return peekToken.Token;
        }

        private JsContext CurrentPositionContext()
        {
            return m_currentToken.FlattenToStart();
        }

        //---------------------------------------------------------------------------------------
        // ReportError
        //
        //  Generate a parser error.
        //  When no context is provided the token is missing so the context is the current position
        //---------------------------------------------------------------------------------------
        private void ReportError(JsError errorId)
        {
            ReportError(errorId, false);
        }

        //---------------------------------------------------------------------------------------
        // ReportError
        //
        //  Generate a parser error.
        //  When no context is provided the token is missing so the context is the current position
        //  The function is told whether or not next call to GetToken() should return the same
        //  token or not
        //---------------------------------------------------------------------------------------
        private void ReportError(JsError errorId, bool skipToken)
        {
            // get the current position token
            JsContext context = m_currentToken.Clone();
            ReportError(errorId, context, skipToken);
        }

        //---------------------------------------------------------------------------------------
        // ReportError
        //
        //  Generate a parser error.
        //  The function is told whether or not next call to GetToken() should return the same
        //  token or not
        //---------------------------------------------------------------------------------------
        private void ReportError(JsError errorId, JsContext context, bool skipToken)
        {
            Debug.Assert(context != null);
            int previousSeverity = m_severity;
            m_severity = JsException.GetSeverity(errorId);
            // EOF error is special and it's the last error we can possibly get
            if (JsToken.EndOfFile == context.Token)
                EOFError(errorId); // EOF context is special
            else
            {
                // report the error if not in error condition and the
                // error for this token is not worse than the one for the
                // previous token
                if (m_goodTokensProcessed > 0 || m_severity < previousSeverity)
                    context.HandleError(errorId);

                // reset proper info
                if (skipToken)
                    m_goodTokensProcessed = -1;
                else
                {
                    m_useCurrentForNext = true;
                    m_goodTokensProcessed = 0;
                }
            }
        }

        //---------------------------------------------------------------------------------------
        // EOFError
        //
        //  Create a context for EOF error. The created context points to the end of the source
        //  code. Assume the the scanner actually reached the end of file
        //---------------------------------------------------------------------------------------
        private void EOFError(JsError errorId)
        {
            JsContext eofCtx = m_currentToken.Clone();
            eofCtx.StartLineNumber = m_scanner.CurrentLine;
            eofCtx.StartLinePosition = m_scanner.StartLinePosition;
            eofCtx.EndLineNumber = eofCtx.StartLineNumber;
            eofCtx.EndLinePosition = eofCtx.StartLinePosition;
            eofCtx.StartPosition = m_document.Source.Length;
            eofCtx.EndPosition++;
            eofCtx.HandleError(errorId);
        }

        //---------------------------------------------------------------------------------------
        // SkipTokensAndThrow
        //
        //  Skip tokens until one in the no skip set is found.
        //  A call to this function always ends in a throw statement that will be caught by the
        //  proper rule
        //---------------------------------------------------------------------------------------
        private void SkipTokensAndThrow()
        {
            SkipTokensAndThrow(null);
        }

        private void SkipTokensAndThrow(JsAstNode partialAST)
        {
            m_useCurrentForNext = false; // make sure we go to the next token
            bool checkForEndOfLine = m_noSkipTokenSet.HasToken(JsToken.EndOfLine);
            while (!m_noSkipTokenSet.HasToken(m_currentToken.Token))
            {
                if (checkForEndOfLine)
                {
                    if (m_foundEndOfLine)
                    {
                        m_useCurrentForNext = true;
                        throw new RecoveryTokenException(JsToken.EndOfLine, partialAST);
                    }
                }
                GetNextToken();
                if (++m_tokensSkipped > c_MaxSkippedTokenNumber)
                {
                    m_currentToken.HandleError(JsError.TooManyTokensSkipped);
                    throw new EndOfStreamException();
                }
                if (JsToken.EndOfFile == m_currentToken.Token)
                    throw new EndOfStreamException();
            }

            m_useCurrentForNext = true;
            // got a token in the no skip set, throw
            throw new RecoveryTokenException(m_currentToken.Token, partialAST);
        }

        //---------------------------------------------------------------------------------------
        // IndexOfToken
        //
        //  check whether the recovery token is a good one for the caller
        //---------------------------------------------------------------------------------------
        private int IndexOfToken(JsToken[] tokens, RecoveryTokenException exc)
        {
            return IndexOfToken(tokens, exc._token);
        }

        private int IndexOfToken(JsToken[] tokens, JsToken token)
        {
            int i, c;
            for (i = 0, c = tokens.Length; i < c; i++)
                if (tokens[i] == token)
                    break;
            if (i >= c)
                i = -1;
            else
            {
                // assume that the caller will deal with the token so move the state back to normal
                m_useCurrentForNext = false;
            }
            return i;
        }

        private bool TokenInList(JsToken[] tokens, JsToken token)
        {
            return (-1 != IndexOfToken(tokens, token));
        }

        private bool TokenInList(JsToken[] tokens, RecoveryTokenException exc)
        {
            return (-1 != IndexOfToken(tokens, exc._token));
        }

        // helper classes
        //***************************************************************************************
        //
        //***************************************************************************************

        // this is a private exception used by the parser to handle syntax errors and partially-computed
        // AST nodes. It will never make it outside the parser, so forget about serializing and proper constructors,
        // and being public to begin with. We KNOW this will never get out because we have a try/catch in the
        // Parse method that will catch any strays that happen to make it out and throw a syntax error.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2237:MarkISerializableTypesWithSerializable"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1032:ImplementStandardExceptionConstructors"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1064:ExceptionsShouldBePublic")]
        private sealed class RecoveryTokenException : Exception
        {
            internal JsToken _token;
            internal JsAstNode _partiallyComputedNode;

            internal RecoveryTokenException() { }

            internal RecoveryTokenException(JsToken token, JsAstNode partialAST)
                : base()
            {
                _token = token;
                _partiallyComputedNode = partialAST;
            }
        }

        //***************************************************************************************
        // NoSkipTokenSet
        //
        //  This class is a possible implementation of the no skip token set. It relies on the
        //  fact that the array passed in are static. Should you change it, this implementation
        //  should change as well.
        //  It keeps a linked list of token arrays that are passed in during parsing, on error
        //  condition the list is traversed looking for a matching token. If a match is found
        //  the token should not be skipped and an exception is thrown to let the proper
        //  rule deal with the token
        //***************************************************************************************
        private class NoSkipTokenSet
        {
            private List<JsToken[]> m_tokenSetList;

            internal NoSkipTokenSet()
            {
                m_tokenSetList = new List<JsToken[]>();
            }

            internal void Add(JsToken[] tokens)
            {
                m_tokenSetList.Add(tokens);
            }

            internal void Remove(JsToken[] tokens)
            {
                bool wasRemoved = m_tokenSetList.Remove(tokens);
                Debug.Assert(wasRemoved, "Token set not in no-skip list");
            }

            internal bool HasToken(JsToken token)
            {
                foreach (JsToken[] tokenSet in m_tokenSetList)
                {
                    for (int ndx = 0; ndx < tokenSet.Length; ++ndx)
                    {
                        if (tokenSet[ndx] == token)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // list of static no skip token set for specifc rules
            internal static readonly JsToken[] s_ArrayInitNoSkipTokenSet = new JsToken[] { JsToken.RightBracket, JsToken.Comma };
            internal static readonly JsToken[] s_BlockConditionNoSkipTokenSet = new JsToken[] { JsToken.RightParenthesis, JsToken.LeftCurly, JsToken.EndOfLine };
            internal static readonly JsToken[] s_BlockNoSkipTokenSet = new JsToken[] { JsToken.RightCurly };
            internal static readonly JsToken[] s_BracketToken = new JsToken[] { JsToken.RightBracket };
            internal static readonly JsToken[] s_CaseNoSkipTokenSet = new JsToken[] { JsToken.Case, JsToken.Default, JsToken.Colon, JsToken.EndOfLine };
            internal static readonly JsToken[] s_DoWhileBodyNoSkipTokenSet = new JsToken[] { JsToken.While };
            internal static readonly JsToken[] s_EndOfLineToken = new JsToken[] { JsToken.EndOfLine };
            internal static readonly JsToken[] s_EndOfStatementNoSkipTokenSet = new JsToken[] { JsToken.Semicolon, JsToken.EndOfLine };
            internal static readonly JsToken[] s_ExpressionListNoSkipTokenSet = new JsToken[] { JsToken.Comma };
            internal static readonly JsToken[] s_FunctionDeclNoSkipTokenSet = new JsToken[] { JsToken.RightParenthesis, JsToken.LeftCurly, JsToken.Comma };
            internal static readonly JsToken[] s_IfBodyNoSkipTokenSet = new JsToken[] { JsToken.Else };
            internal static readonly JsToken[] s_MemberExprNoSkipTokenSet = new JsToken[] { JsToken.LeftBracket, JsToken.LeftParenthesis, JsToken.AccessField };
            internal static readonly JsToken[] s_NoTrySkipTokenSet = new JsToken[] { JsToken.Catch, JsToken.Finally };
            internal static readonly JsToken[] s_ObjectInitNoSkipTokenSet = new JsToken[] { JsToken.RightCurly, JsToken.Comma };
            internal static readonly JsToken[] s_ParenExpressionNoSkipToken = new JsToken[] { JsToken.RightParenthesis };
            internal static readonly JsToken[] s_ParenToken = new JsToken[] { JsToken.RightParenthesis };
            internal static readonly JsToken[] s_PostfixExpressionNoSkipTokenSet = new JsToken[] { JsToken.Increment, JsToken.Decrement };
            internal static readonly JsToken[] s_StartStatementNoSkipTokenSet = new JsToken[]{JsToken.LeftCurly,
                                                                                               JsToken.Var,
                                                                                               JsToken.Const,
                                                                                               JsToken.If,
                                                                                               JsToken.For,
                                                                                               JsToken.Do,
                                                                                               JsToken.While,
                                                                                               JsToken.With,
                                                                                               JsToken.Switch,
                                                                                               JsToken.Try};
            internal static readonly JsToken[] s_SwitchNoSkipTokenSet = new JsToken[] { JsToken.Case, JsToken.Default };
            internal static readonly JsToken[] s_TopLevelNoSkipTokenSet = new JsToken[] { JsToken.Function };
            internal static readonly JsToken[] s_VariableDeclNoSkipTokenSet = new JsToken[] { JsToken.Comma, JsToken.Semicolon };
        }
    }
}
