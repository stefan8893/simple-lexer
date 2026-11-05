
using SimpleLexer;

var lexer = new SimpleLexer.SimpleLexer("AB eq (CD AND foo:eq:'ab\\'c\\n:d') \"xy:'z'\"", new SimpleLexerConfiguration
{
    Delimiters = [':']
});

foreach (var token in lexer.Tokenize())
{
    Console.WriteLine("value: '{0}' -> type: {1}", token.Value, token.Type);
}