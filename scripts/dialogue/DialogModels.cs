#nullable enable

namespace Tts;

public sealed record YarnLine(
	string Speaker,
	string Descriptor,
	MarkupParser.ParseResult ParsedText
);

public sealed record YarnOption(string Text, int DialogueOptionID);
