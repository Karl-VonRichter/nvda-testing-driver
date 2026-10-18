using System;
using System.Threading.Tasks;

namespace SampleTests
{
	internal static class EscapeClosure
	{
		// Speech such as "Browse mode" does not establish closure. Check the actual dialog after each Escape.
		internal static async Task<int> CountUntilClosedAsync(Func<Task> sendEscape, Func<bool> isOpen, int maximumEscapes = 2)
		{
			if (maximumEscapes < 1) throw new ArgumentOutOfRangeException(nameof(maximumEscapes));
			if (!isOpen()) throw new InvalidOperationException("The dialog must be open before measuring Escape closure.");
			for (int count = 1; count <= maximumEscapes; count++)
			{
				await sendEscape();
				if (!isOpen()) return count;
			}
			throw new InvalidOperationException("The dialog remained open after the configured Escape sequence.");
		}
	}
}
