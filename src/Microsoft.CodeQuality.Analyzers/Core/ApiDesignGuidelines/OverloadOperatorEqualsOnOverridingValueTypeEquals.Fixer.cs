﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;
using static System.Collections.Immutable.ImmutableArrayExtensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA2231: Overload operator equals on overriding ValueType.Equals
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, LanguageNames.VisualBasic), Shared]
    public sealed class OverloadOperatorEqualsOnOverridingValueTypeEqualsFixer : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(OverloadOperatorEqualsOnOverridingValueTypeEqualsAnalyzer.RuleId);

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(context.Document);
            SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            SyntaxNode declaration = root.FindNode(context.Span);
            declaration = generator.GetDeclaration(declaration);
            if (declaration == null)
            {
                return;
            }

            SemanticModel model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (!(model.GetDeclaredSymbol(declaration) is INamedTypeSymbol typeSymbol))
            {
                return;
            }

            // We cannot have multiple overlapping diagnostics of this id.
            Diagnostic diagnostic = context.Diagnostics.Single();
            string title = MicrosoftApiDesignGuidelinesAnalyzersResources.OverloadOperatorEqualsOnOverridingValueTypeEqualsTitle;
            context.RegisterCodeFix(
                new MyCodeAction(title,
                    async ct => await ImplementOperatorEquals(context.Document, declaration, typeSymbol, ct).ConfigureAwait(false),
                    equivalenceKey: title), diagnostic);
        }

        private static async Task<Document> ImplementOperatorEquals(Document document, SyntaxNode declaration, INamedTypeSymbol typeSymbol, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.EqualityOperatorName))
            {
                var equalityOperator = generator.DefaultOperatorEqualityDeclaration(typeSymbol);

                editor.AddMember(declaration, equalityOperator);
            }

            if (!typeSymbol.ImplementsOperator(WellKnownMemberNames.InequalityOperatorName))
            {
                var inequalityOperator = generator.DefaultOperatorInequalityDeclaration(typeSymbol);

                editor.AddMember(declaration, inequalityOperator);
            }

            return editor.GetChangedDocument();
        }

        private class MyCodeAction : DocumentChangeAction
        {
            public MyCodeAction(string title, Func<CancellationToken, Task<Document>> createChangedDocument, string equivalenceKey)
                : base(title, createChangedDocument, equivalenceKey)
            {
            }
        }

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }
    }
}
