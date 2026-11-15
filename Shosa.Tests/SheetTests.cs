using Shosa.Core;
using Shosa.Core.Output;

namespace Shosa.Tests;

public class SheetTests
{
    private const uint SectionColor = 0x7A4FBF, Faint = 0x9AA1AB;

    private static Canvas Sheet(string text) =>
        ContactSheet.Render(Assert.Single(ShosaCompilation.Compile(text).Clips));

    private static int Count(Canvas canvas, uint color, int x0, int y0, int x1, int y1)
    {
        int count = 0;
        for (int y = y0; y < Math.Min(y1, canvas.Height); y++)
        {
            for (int x = x0; x < Math.Min(x1, canvas.Width); x++)
            {
                if (canvas.ColorAt(x, y) == color)
                    count++;
            }
        }
        return count;
    }

    [Fact]
    public void A_clip_of_several_sections_names_each_where_it_begins()
    {
        const string sections = "tempo 100\n" + Samples.SoloCast + "score { dancer solo }\n"
            + "section A { solo { arms { up:4 } } }\nsection B { solo { arms { down:4 } } }\n";
        var one = Sheet(sections + "form { A }\n");
        var two = Sheet(sections + "form { A B }\n");

        // One section: no name on the first line. The tempo is under the beat of the first pose, and nowhere else.
        Assert.Equal(0, Count(one, SectionColor, 0, 0, one.Width, 30));
        Assert.True(Count(one, SectionColor, 14, 32, 204, 50) > 0);
        Assert.Equal(0, Count(one, SectionColor, 204, 0, one.Width, one.Height));
        Assert.Equal(0, Count(two, SectionColor, 14, 296, 204, 314));   // B goes on at A's tempo: not said again
        // 8 beats at a pose per half beat, 8 to a row: A is named in the first cell, B at beat 4 (the first cell of the second row).
        Assert.True(Count(two, SectionColor, 14, 14, 204, 30) > 0);
        Assert.True(Count(two, SectionColor, 14, 278, 204, 294) > 0);
        Assert.Equal(0, Count(two, SectionColor, 204, 14, two.Width, 30));
    }

    [Fact]
    public void Several_dancers_have_their_names_under_them()
    {
        const string cast = "tempo 100\nrole f\ndancer ann humanoid on f at -1 0\ndancer ben humanoid on f at 1 0\n"
            + "section A { f { arms { up:4 } } }\nform { A }\n";
        // The same file with one of its dancers in the score, and with both.
        const string solo = cast + "score { dancer ann }\n";
        const string duo = cast + "score { dancer ann  dancer ben }\n";

        // Under the floor line of the first cell (the floor is 40 pixels above the cell's bottom, at 238).
        Assert.Equal(0, Count(Sheet(solo), Faint, 14, 240, 204, 276));
        Assert.True(Count(Sheet(duo), Faint, 14, 240, 204, 276) > 20);
    }

    [Fact]
    public void Text_is_drawn_as_written_capitals_and_small_letters_apart()
    {
        // Every printable character has a shape of its own, inside its cell of 6 by 8 dots.
        var shapes = new HashSet<string>();
        for (char ch = '!'; ch <= '~'; ch++)
        {
            var canvas = new Canvas(8, 10);
            canvas.Text(1, 1, ch.ToString(), 1, 0);
            var dots = new System.Text.StringBuilder();
            for (int y = 0; y < canvas.Height; y++)
            {
                for (int x = 0; x < canvas.Width; x++)
                {
                    bool on = canvas.ColorAt(x, y) == 0;
                    Assert.False(on && (x < 1 || x > 5 || y < 1 || y > 8), $"'{ch}' leaves its cell");
                    dots.Append(on ? '#' : '.');
                }
            }
            Assert.True(shapes.Add(dots.ToString()), $"'{ch}' looks like another character");
        }

        // N: two uprights and the stroke from the top left to the bottom right.
        var n = new Canvas(5, 7);
        n.Text(0, 0, "N", 1, 0);
        string Row(int y) => string.Concat(Enumerable.Range(0, 5).Select(x => n.ColorAt(x, y) == 0 ? '#' : '.'));
        Assert.Equal(new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" }, Enumerable.Range(0, 7).Select(Row));
        Assert.Equal(34, Canvas.TextWidth("abc", 2));
    }
}
