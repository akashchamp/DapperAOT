using System;
using System.Collections.Generic;
using Xunit;

namespace Dapper.AOT.Test;

/// <summary>
/// <c>{=name}</c> literal injection renders values as SQL text rather than binding them, so any
/// divergence from vanilla is a silent wrong-query rather than an error. These assert parity against
/// vanilla's own <c>SqlMapper.Format</c> rather than against hand-written expectations, so the two
/// cannot drift.
/// </summary>
public class LiteralFormatTests
{
    // Format is protected (generated code is a CommandFactory subclass); reach it the same way
    private sealed class Probe : CommandFactory<object?>
    {
        public static string Call(object? value) => Format(value);
        public static string Call(bool value) => Format(value);
        public static string Call(int value) => Format(value);
        public static string Call(decimal value) => Format(value);
    }

    public static IEnumerable<object[]> Values => [
        [null!], [true], [false],
        [(byte)1], [(sbyte)-1], [(short)-2], [(ushort)2], [3], [(uint)4], [5L], [(ulong)6],
        [1.5f], [2.5d], [123.45M], [int.MinValue], [long.MaxValue],
        [AnEnum.B], [AnotherEnum.A],
        [new[] { 1, 3, 4 }], [Array.Empty<int>()], [new[] { true, false }],
        [new List<decimal> { 1.5M, 2M }],
    ];

    [Theory, MemberData(nameof(Values))]
    public void MatchesVanilla(object? value)
    {
#pragma warning disable CS0618 // vanilla's Format is obsolete; that is exactly what we are pinning against
        var expected = SqlMapper.Format(value);
#pragma warning restore CS0618
        Assert.Equal(expected, Probe.Call(value));
    }

    [Theory]
    [InlineData("some text")]        // strings are deliberately not literal-injectable
    [InlineData('c')]
    public void RefusesUninjectableTypes(object value)
        => Assert.Throws<NotSupportedException>(() => Probe.Call(value));

    [Fact] // the typed fast paths must agree with the object path, or generated code diverges by shape
    public void TypedOverloadsAgreeWithObjectPath()
    {
        Assert.Equal(Probe.Call((object)true), Probe.Call(true));
        Assert.Equal(Probe.Call((object)false), Probe.Call(false));
        Assert.Equal(Probe.Call((object)42), Probe.Call(42));
        Assert.Equal(Probe.Call((object)123.45M), Probe.Call(123.45M));
    }

    public enum AnEnum { A = 1, B = 2 }
    public enum AnotherEnum : byte { A = 1, B = 2 }
}
