﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SupersonicSound
{
    /// <summary>
    /// Debugging helper. This attribute indicates that the enum it is applied to should be equivalent to an enum in the FMOD wrapper
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    internal class EquivalentEnumAttribute
        : Attribute
    {
        public Type Equivalent { get; private set; }

        public string[] MissingMembers { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="equivalent">The underlying enum type in the FMOD wrapper which this enum should be equivalent to</param>
        /// <param name="missingMembers">Names of members in the FMOD wrapper which are allowed to be missing</param>
        public EquivalentEnumAttribute(Type equivalent, params string[] missingMembers)
        {
            if (!equivalent.IsEnum)
                throw new ArgumentException("equivalent");

            Equivalent = equivalent;
            MissingMembers = missingMembers;
        }

        public void Validate(Type checkAgainst)
        {
            if (!checkAgainst.IsEnum)
                throw new ArgumentException("checkAgainst");

            if (Enum.GetUnderlyingType(Equivalent) != Enum.GetUnderlyingType(checkAgainst))
                throw new Exception(string.Format("Incompatible - Enum underlying types are not equivalent between enum {0} and base enum {1} ", checkAgainst, Equivalent));

            GetType()
                .GetMethod("ValidateGeneric", BindingFlags.NonPublic | BindingFlags.Instance)
                .MakeGenericMethod(Enum.GetUnderlyingType(checkAgainst))
                .Invoke(this, new object[] { checkAgainst });
        }

        // ReSharper disable once UnusedMember.Local
        private void ValidateGeneric<U>(Type checkAgainst)
        {
            var actualValues = Enum.GetValues(checkAgainst).Cast<U>().ToArray();
            var expectedValues = Enum.GetValues(Equivalent).Cast<U>().ToArray().Except(MissingMembers.Select(GetValueFromName<U>));

            var unexpectedValues = actualValues.Where(n => !expectedValues.Contains(n)).ToArray();
            var unexpectedNames = unexpectedValues.Select(v => Enum.GetName(checkAgainst, v)).ToArray();
            if (unexpectedValues.Length > 0)
            {
                // THIS MEANS:
                // Your enum (which is *meant* to be equivalent to some FMOD enum) has a value which FMOD does not have
                // Check this array (unexpectedNames) to see which names should not be there
                Console.WriteLine(unexpectedNames);

                throw new Exception(string.Format("Incompatible - Found {0} names in enum {1} which are not in base enum {2}", unexpectedValues.Length, checkAgainst.Name, Equivalent.Name));
            }

            var unmatchedValues = expectedValues.Where(n => !actualValues.Contains(n)).ToArray();
            var unmatchedNames = unmatchedValues.Select(v => Enum.GetName(Equivalent, v)).ToArray();
            if (unmatchedValues.Length > 0)
            {
                // THIS MEANS:
                // Your enum (which is *meant* to be equivalent to some FMOD enum) is missing a value which FMOD has
                // Check this array (unmatchedNames) to see which names are missing
                unmatchedNames.ToList().ForEach(x => Console.WriteLine(x));

                throw new Exception(string.Format("Incompatible - Found {0} names in base enum {1} which are not in enum {2}", unmatchedValues.Length, Equivalent.Name, checkAgainst.Name));
            }
        }

        private U GetValueFromName<U>(string name)
        {
            return (U)Enum.Parse(Equivalent, name);
        }
    }

    static class EquivalentEnum<TIn, TOut>
        where TIn : struct, IConvertible
        where TOut : struct
    {
        public static readonly Func<TIn, TOut> Cast = GenerateConverter();

        static EquivalentEnum()
        {
            //Basic sanity check, are we working with enums?
            if (!typeof(TIn).IsEnum)
                throw new Exception("CastEquivalentEnum input type must be an enum (programmer error)");
            if (!typeof(TOut).IsEnum)
                throw new Exception("CastEquivalentEnum output type must be an enum (programmer error)");

            //Is the input an equivalent enum?
            var equiv = typeof(TIn).GetCustomAttributes(typeof(EquivalentEnumAttribute), true).Cast<EquivalentEnumAttribute>().SingleOrDefault();
            if (equiv == null)
                throw new Exception("CastEquivalentEnum input type must have EquivalentEnum Attribute (programmer error)");

            //Is the output the correct type (as defined by equivalence)
            if (!equiv.Equivalent.IsAssignableFrom(typeof(TOut)))
                throw new Exception("CastEquivalentEnum output type must be the equivalent enum of input type (programmer error)");

            //consider checking if all the values in input are valid values of output - this is complex due to flags enums mixing stuff up
            //This checking would have to be added into the converter
        }

        static Func<TIn, TOut> GenerateConverter()
        {
            var parameter = Expression.Parameter(typeof(TIn));
            var dynamicMethod = Expression.Lambda<Func<TIn, TOut>>(
                Expression.ConvertChecked(parameter, typeof(TOut)),
                parameter
            );
            return dynamicMethod.Compile();
        }
    }
}
