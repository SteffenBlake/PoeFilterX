using PoeFilterX.Business;
using PoeFilterX.Business.Extensions;
using System.ComponentModel.DataAnnotations;

namespace PoeFilterX.Tests
{
    public class Enum_GetDisplayName_Tests
    {
        public enum TestEnum
        {
            A,
            [Display(Name = "1")]
            B,
            [Display(Name = "A")]
            C,
            [Display(Name = "")]
            D,
            [Display(Name = "!=/?'`\\\"()*&^%$#@!~:;,.<>[]{}|")]
            E,
            [Display(Name = "null")]
            F,

        }


        [TestCase(TestEnum.A, ExpectedResult = "A")]
        [TestCase(TestEnum.B, ExpectedResult = "1")]
        [TestCase(TestEnum.C, ExpectedResult = "A")]
        [TestCase(TestEnum.D, ExpectedResult = "")]
        [TestCase(TestEnum.E, ExpectedResult = "!=/?'`\\\"()*&^%$#@!~:;,.<>[]{}|")]
        [TestCase(TestEnum.F, ExpectedResult = "null")]
        public string Enum_GetDisplayName_ByCase(TestEnum value)
        {
            return value.GetDisplayName();
        }
    }
}