using System.Text.Json;

namespace Moss.Core;

public static class Json
{
	public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		MaxDepth = 16
	};
}
