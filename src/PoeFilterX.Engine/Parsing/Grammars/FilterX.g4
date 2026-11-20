grammar FilterX;

document: statement* EOF;
statement: importStatement | block | command;
importStatement: USING argument SEMICOLON;
block: OPEN_BRACE statement* CLOSE_BRACE | OPEN_BRACKET statement* CLOSE_BRACKET;
command: ATOM argument* SEMICOLON;
argument: ATOM | STRING | USING | OPERATOR;

USING: [uU][sS][iI][nN][gG];
OPEN_BRACE: '{';
CLOSE_BRACE: '}';
OPEN_BRACKET: '[';
CLOSE_BRACKET: ']';
SEMICOLON: ';';
OPERATOR: '==' | '>=' | '<=' | '=' | '>' | '<' | '!';
STRING: '"' ('\\' ["\\nt] | ~["\\])* '"';
// These tokens preserve malformed literals for precise diagnostics and editor recovery.
UNTERMINATED_STRING: '"' ('\\' . | ~["\\])* '\\'? EOF;
INVALID_STRING: '"' ('\\' . | ~["\\])* '"';
COMMENT: '#' ~[\r\n]* -> channel(HIDDEN);
WHITESPACE: [ \t\r\n\uFEFF]+ -> channel(HIDDEN);
ATOM: ~[ \t\r\n\uFEFF{}\u005B\u005D;"#\\=<>!]+;
INVALID: .;
