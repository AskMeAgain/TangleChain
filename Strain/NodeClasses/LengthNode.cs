﻿using System.Collections.Generic;
using Strain.Classes;
using TangleChainIXI.Smartcontracts;

namespace Strain.NodeClasses
{
    public class LengthNode : Node
    {

        public string Name { get; set; }

        public LengthNode(string name)
        {
            Name = name;
        }

        public override List<Expression> Compile(Scope scope, ParserContext context)
        {
            return new ValueNode((scope.ArrayIndex[Name] + 1).ToString()).Compile(scope, context.NewContext("ArrayLength"));
        }
    }
}
