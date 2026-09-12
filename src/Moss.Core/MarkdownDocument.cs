using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Moss.Core;

public static class MarkdownDocument
{
	private static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().DisableHtml().Build();

	public static IReadOnlyList<TextRun> Render(string text)
	{
		if (text.Length > 1000000)
		{
			throw new InvalidDataException("Note too long.");
		}
		List<TextRun> result = new List<TextRun>();
		Blocks(Markdown.Parse(text, pipeline));
		return result;
		void Blocks(ContainerBlock blocks, int indent = 0)
		{
			foreach (Block block in blocks)
			{
				if (!(block is HeadingBlock headingBlock))
				{
					if (!(block is CodeBlock codeBlock))
					{
						if (!(block is ListBlock listBlock))
						{
							if (!(block is QuoteBlock blocks2))
							{
								if (!(block is LeafBlock leafBlock))
								{
									if (block is ContainerBlock blocks3)
									{
										Blocks(blocks3, indent);
									}
								}
								else
								{
									Inline(leafBlock.Inline?.FirstChild);
									result.Add(new TextRun("\n\n"));
								}
							}
							else
							{
								result.Add(new TextRun("│ ", Bold: false, Italic: true));
								Blocks(blocks2, indent);
							}
						}
						else
						{
							int num = 1;
							foreach (ListItemBlock item in listBlock.OfType<ListItemBlock>())
							{
								result.Add(new TextRun(new string(' ', indent * 2) + (listBlock.IsOrdered ? $"{num++}. " : "• ")));
								Blocks(item, indent + 1);
							}
						}
					}
					else
					{
						result.Add(new TextRun(codeBlock.Lines.ToString() + "\n\n", Bold: false, Italic: false, Code: true));
					}
				}
				else
				{
					Inline(headingBlock.Inline?.FirstChild, bold: true, italic: false, headingBlock.Level);
					result.Add(new TextRun("\n\n"));
				}
			}
		}
		void Inline(Inline? node, bool bold = false, bool italic = false, int heading = 0, string? link = null)
		{
			for (Inline inline = node; inline != null; inline = inline.NextSibling)
			{
				if (!(inline is LiteralInline literalInline))
				{
					if (!(inline is CodeInline codeInline))
					{
						if (!(inline is EmphasisInline emphasisInline))
						{
							if (!(inline is LinkInline linkInline))
							{
								if (!(inline is LineBreakInline))
								{
									if (inline is ContainerInline containerInline)
									{
										Inline(containerInline.FirstChild, bold, italic, heading, link);
									}
								}
								else
								{
									result.Add(new TextRun("\n"));
								}
							}
							else
							{
								Uri result2;
								bool flag = Uri.TryCreate(linkInline.Url, UriKind.Absolute, out result2);
								if (flag)
								{
									string scheme = result2.Scheme;
									bool flag2 = ((scheme == "https" || scheme == "http") ? true : false);
									flag = flag2;
								}
								string text2 = (flag ? linkInline.Url : null);
								Inline(linkInline.FirstChild, bold, italic, heading, linkInline.IsImage ? null : text2);
							}
						}
						else
						{
							Inline(emphasisInline.FirstChild, bold || emphasisInline.DelimiterCount >= 2, italic || emphasisInline.DelimiterCount == 1, heading, link);
						}
					}
					else
					{
						result.Add(new TextRun(codeInline.Content, bold, italic, Code: true, heading));
					}
				}
				else
				{
					result.Add(new TextRun(literalInline.Content.ToString(), bold, italic, Code: false, heading, link));
				}
			}
		}
	}
}
