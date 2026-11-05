using System.Text;

namespace SimpleLexer;

public enum TokenType
{
    BareToken,
    StringLiteral,
    Delimiter,
    Whitespace,
    OpenParenthesis,
    CloseParenthesis
}

public readonly record struct SourcePosition(int Offset, int Line, int Column);

public readonly record struct SourceSpan(SourcePosition Start, SourcePosition End);

public record Token(TokenType Type, string Value, string RawValue, SourceSpan Span);

public class SimpleLexerConfiguration
{
    public char[] Delimiters { get; init; } = [];
}

public class SimpleLexer(string input, SimpleLexerConfiguration lexerConfiguration)
{
    private readonly HashSet<char> _delimiters = [.. lexerConfiguration.Delimiters];
    private int _column = 1;
    private int _line = 1;
    private int _position;

    private SourcePosition CurrentPosition => new(_position, _line, _column);

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_position < input.Length)
        {
            if (char.IsWhiteSpace(input[_position]))
            {
                tokens.Add(ReadWhitespace());
                continue;
            }

            var start = CurrentPosition;
            var character = input[_position];

            switch (character)
            {
                case '(':
                    tokens.Add(ReadParenthesis(TokenType.OpenParenthesis));
                    break;
                case ')':
                    tokens.Add(ReadParenthesis(TokenType.CloseParenthesis));
                    break;
                case '\'':
                case '\"':
                    tokens.Add(ReadQuotedText());
                    break;
                default:
                    tokens.Add(_delimiters.Contains(character)
                        ? ReadDelimiter()
                        : ReadWord(start));
                    break;
            }
        }

        return tokens;
    }

    private Token ReadWhitespace()
    {
        var start = CurrentPosition;
        var valueStart = _position;

        while (_position < input.Length && char.IsWhiteSpace(input[_position]))
            Advance();

        var value = input[valueStart.._position];
        return new Token(TokenType.Whitespace, value, value, new SourceSpan(start, CurrentPosition));
    }

    private Token ReadParenthesis(TokenType type)
    {
        var start = CurrentPosition;
        var value = input[_position].ToString();
        Advance();

        return new Token(type, value, value, new SourceSpan(start, CurrentPosition));
    }

    private Token ReadDelimiter()
    {
        var start = CurrentPosition;
        var value = input[_position].ToString();
        Advance();

        return new Token(TokenType.Delimiter, value, value, new SourceSpan(start, CurrentPosition));
    }

    private Token ReadQuotedText()
    {
        var start = CurrentPosition;
        var quote = input[_position];
        Advance();
        var value = new StringBuilder();

        while (_position < input.Length)
        {
            var character = input[_position];
            if (character == quote)
            {
                Advance();
                var span = new SourceSpan(start, CurrentPosition);
                var rawValue = input[span.Start.Offset..span.End.Offset];
                return new Token(TokenType.StringLiteral, value.ToString(), rawValue, span);
            }

            if (character != '\\')
            {
                value.Append(character);
                Advance();
                continue;
            }

            var escapeStart = CurrentPosition;
            Advance();

            if (_position == input.Length)
            {
                var escapeSpan = new SourceSpan(escapeStart, CurrentPosition);
                throw new FormatException(FormatDiagnostic("Unterminated escape sequence", escapeSpan));
            }

            var escapedCharacter = input[_position];
            Advance();
            var escapedValue = escapedCharacter switch
            {
                '\\' => '\\',
                '\'' => '\'',
                '"' => '"',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                '0' => '\0',
                _ => throw new FormatException(FormatDiagnostic(
                    $"Invalid escape sequence '\\{escapedCharacter}'",
                    new SourceSpan(escapeStart, CurrentPosition)))
            };
            value.Append(escapedValue);
        }

        var errorSpan = new SourceSpan(start, CurrentPosition);
        throw new FormatException(FormatDiagnostic("Unterminated quoted string", errorSpan));
    }

    private Token ReadWord(SourcePosition start)
    {
        var valueStart = _position;
        while (_position < input.Length &&
               !char.IsWhiteSpace(input[_position]) &&
               !_delimiters.Contains(input[_position]) &&
               input[_position] is not '(' and not ')' and not '\'' and not '"')
            Advance();

        var value = input[valueStart.._position];
        return new Token(TokenType.BareToken, value, value, new SourceSpan(start, CurrentPosition));
    }

    private void Advance()
    {
        switch (input[_position])
        {
            case '\r':
                _position += _position + 1 < input.Length && input[_position + 1] == '\n' ? 2 : 1;
                _line++;
                _column = 1;
                break;
            case '\n':
                _position++;
                _line++;
                _column = 1;
                break;
            default:
                _position++;
                _column++;
                break;
        }
    }

    private string FormatDiagnostic(string message, SourceSpan span)
    {
        var (sourceExcerpt, markerColumn) = GetSourceExcerpt(span);
        var marker = new string(' ', markerColumn) + '↑';

        return $"""
                {message} at line {span.Start.Line}, column {span.Start.Column}.
                {sourceExcerpt}{Environment.NewLine}{marker}
                """;
    }

    private (string Text, int MarkerColumn) GetSourceExcerpt(SourceSpan span)
    {
        var lineStart = input.AsSpan(0, span.Start.Offset).LastIndexOfAny('\r', '\n') + 1;
        var lineEndOffset = input.AsSpan(span.Start.Offset).IndexOfAny('\r', '\n');
        var lineEnd = lineEndOffset < 0 ? input.Length : span.Start.Offset + lineEndOffset;

        const int contextBefore = 15;
        const int contextAfter = 15;

        var excerptStart = Math.Max(lineStart, span.Start.Offset - contextBefore);
        var excerptEnd = Math.Min(lineEnd, span.Start.Offset + contextAfter);
        var hasLeadingOmission = excerptStart > lineStart;
        var hasTrailingOmission = excerptEnd < lineEnd;

        var text =
            $"{(hasLeadingOmission ? "…" : "")}{input[excerptStart..excerptEnd]}{(hasTrailingOmission ? "…" : "")}";
        var markerColumn = (hasLeadingOmission ? 1 : 0) + span.Start.Offset - excerptStart;

        return (text, markerColumn);
    }
}