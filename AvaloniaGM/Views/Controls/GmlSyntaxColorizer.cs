using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace AvaloniaGM.Views.Controls
{
    internal sealed partial class GmlSyntaxColorizer : DocumentColorizingTransformer
    {
        private static readonly IBrush KeywordBrush = new SolidColorBrush(Color.Parse("#FF004A9F"));
        private static readonly IBrush FunctionBrush = new SolidColorBrush(Color.Parse("#FF6D28D9"));
        private static readonly IBrush ConstantBrush = new SolidColorBrush(Color.Parse("#FF0F766E"));
        private static readonly IBrush NumberBrush = new SolidColorBrush(Color.Parse("#FF9A3412"));
        private static readonly IBrush StringBrush = new SolidColorBrush(Color.Parse("#FFB45309"));
        private static readonly IBrush CommentBrush = new SolidColorBrush(Color.Parse("#FF6B7280"));
        private static readonly IBrush DirectiveBrush = new SolidColorBrush(Color.Parse("#FFB91C1C"));

        private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
        {
            "if", "else", "while", "do", "for", "repeat", "switch", "case", "default",
            "break", "continue", "return", "exit", "with", "var", "globalvar", "enum",
            "try", "catch", "finally", "throw", "new", "delete", "not", "and", "or",
            "xor", "div", "mod", "begin", "end", "then", "until"
        };

        private static readonly HashSet<string> Constants = new(StringComparer.Ordinal)
        {
            "true", "false", "self", "other", "all", "noone", "global", "local", "undefined", "pointer_null"
        };

        protected override void ColorizeLine(DocumentLine line)
        {
            var lineStartOffset = line.Offset;
            var lineText = CurrentContext.Document.GetText(line);

            foreach (Match match in DirectiveRegex().Matches(lineText))
            {
                ApplyBrush(lineStartOffset + match.Index, match.Length, DirectiveBrush);
            }

            foreach (Match match in CommentRegex().Matches(lineText))
            {
                ApplyBrush(lineStartOffset + match.Index, match.Length, CommentBrush);
            }

            foreach (Match match in StringRegex().Matches(lineText))
            {
                ApplyBrush(lineStartOffset + match.Index, match.Length, StringBrush);
            }

            foreach (Match match in NumberRegex().Matches(lineText))
            {
                ApplyBrush(lineStartOffset + match.Index, match.Length, NumberBrush);
            }

            foreach (Match match in IdentifierRegex().Matches(lineText))
            {
                var identifier = match.Value;
                var offset = lineStartOffset + match.Index;

                if (Keywords.Contains(identifier))
                {
                    ApplyBrush(offset, match.Length, KeywordBrush);
                    continue;
                }

                if (Constants.Contains(identifier))
                {
                    ApplyBrush(offset, match.Length, ConstantBrush);
                    continue;
                }

                if (IsFunctionCall(lineText, match))
                {
                    ApplyBrush(offset, match.Length, FunctionBrush);
                }
            }
        }

        private void ApplyBrush(int startOffset, int length, IBrush foreground)
        {
            if (length <= 0)
            {
                return;
            }

            ChangeLinePart(startOffset, startOffset + length, visualLineElement =>
            {
                visualLineElement.TextRunProperties.SetForegroundBrush(foreground);
            });
        }

        private static bool IsFunctionCall(string lineText, Match identifierMatch)
        {
            var index = identifierMatch.Index + identifierMatch.Length;
            while (index < lineText.Length && char.IsWhiteSpace(lineText[index]))
            {
                index++;
            }

            return index < lineText.Length && lineText[index] == '(';
        }

        [GeneratedRegex(@"^\s*#\w+.*$", RegexOptions.Multiline)]
        private static partial Regex DirectiveRegex();

        [GeneratedRegex(@"//.*$")]
        private static partial Regex CommentRegex();

        [GeneratedRegex("\"([^\"\\\\]|\\\\.)*\"|'([^'\\\\]|\\\\.)*'")]
        private static partial Regex StringRegex();

        [GeneratedRegex(@"\b(?:\$[0-9A-Fa-f]+|0x[0-9A-Fa-f]+|\d+(?:\.\d+)?)\b")]
        private static partial Regex NumberRegex();

        [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*\b")]
        private static partial Regex IdentifierRegex();
    }
}
