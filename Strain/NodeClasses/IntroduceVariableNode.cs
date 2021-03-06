﻿using System.Collections.Generic;
using System.Linq;
using Strain.Classes;
using TangleChainIXI.Smartcontracts;

namespace Strain.NodeClasses
{
    public class IntroduceVariableNode : Node
    {
        public string Name { get; protected set; }

        public IntroduceVariableNode(string name, Node node)
        {
            Name = name;
            Nodes.Add(node);
        }

        public override List<Expression> Compile(Scope scope, ParserContext context)
        {
            context = context.OneContextUp();

            scope.AddVariable(Name, context.ToString());

            var list = new List<Expression>(Nodes.Compile(scope, context));
            var result = list.Last().Args3;

            list.Add(Factory.Copy( result, context + "-" + Name));

            return list;
        }

    }
}
