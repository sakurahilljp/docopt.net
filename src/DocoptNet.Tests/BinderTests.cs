using System.Collections.Generic;
using NUnit.Framework;
using System.Runtime.Serialization;

namespace DocoptNet.Tests
{
    class SimpleOptions
    {
        public bool BoolValue { get; set; }

        public int  IntegerValue { get; set; }
        public long LongValue { get; set; }
        
        public float FloatValue { get; set; }
        
        public double DoubleValue { get; set; }

        public decimal DecimalValue { get; set; }

        public string StringValue { get; set; } 
    }

    class ArrayOptions
    {
        public int[] Integers { get; set; }
        
        public string[] Strings { get; set; }
    }

    [DataContract]
    class DataContractOptions
    {
        [DataMember]
        public string Member { get; set; }

        [DataMember(Name ="Named")]
        public string NamedMember { get; set; }

        public string NonDataMember { get; set; }
    }

    [TestFixture]
    public class BinderTests
    {
        [Test]
        public void Binder_ValuePropery()
        {
            var args = new Dictionary<string, ValueObject>()
            {
                { "BoolValue", new ValueObject(true)},
                { "IntegerValue", new ValueObject("123")},
                { "LongValue", new ValueObject("123123")},
                { "FloatValue", new ValueObject("1.23")},
                { "DoubleValue", new ValueObject("1.23123")},
                { "DecimalValue", new ValueObject("1.333333")},
                { "StringValue", new ValueObject("string value")},
            };

            var binder = new Binder<SimpleOptions>();
            var opts = binder.Bind(args);

            Assert.AreEqual(true, opts.BoolValue);
            Assert.AreEqual(123, opts.IntegerValue);
            Assert.AreEqual(123123L, opts.LongValue);
            Assert.AreEqual(1.23f, opts.FloatValue);
            Assert.AreEqual(1.23123, opts.DoubleValue);
            Assert.AreEqual(1.333333m, opts.DecimalValue);
            Assert.AreEqual("string value", opts.StringValue);

        }
        
        [Test]
        public void Binder_DataContract()
        {
            var args = new Dictionary<string, ValueObject>()
            {
                { "Member", new ValueObject("Member")},
                { "Named", new ValueObject("Named")},
                { "NamedMember", new ValueObject("NamedMember")},
                { "NonDataMember", new ValueObject("NonDataMember")},
            };

            var binder = new Binder<DataContractOptions>();
            var opts = binder.Bind(args);

            Assert.AreEqual("Member", opts.Member);
            Assert.AreEqual("Named", opts.NamedMember);
            Assert.AreEqual(null, opts.NonDataMember);

        }

        [Test]
        public void Binder_ArrayProperty()
        {
            var args = new Dictionary<string, ValueObject>();

            var integers = new ValueObject("1");
            integers.Add(new ValueObject("2"));
            integers.Add(new ValueObject("3"));

            var strings = new ValueObject("S1");
            strings.Add(new ValueObject("S2"));
            strings.Add(new ValueObject("S3"));

            args["Integers"] = integers;
            args["Strings"] = strings;

            var binder = new Binder<ArrayOptions>();
            var opts = binder.Bind(args);
            
            CollectionAssert.AreEqual(new int[] { 1, 2, 3 }, opts.Integers);
            CollectionAssert.AreEqual(new string[] { "S1", "S2", "S3" }, opts.Strings);
        }
    }
}