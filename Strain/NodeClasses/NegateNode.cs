﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StrainLanguage.Classes;
using TangleChainIXI.Smartcontracts;

namespace StrainLanguage.NodeClasses
{
    public class NegateNode : Node
    {
        public NegateNode(Node node)
        {
            Nodes.Add(node);
        }

        public override List<Expression> Compile(Scope scope, ParserContext context)
        {
            var list = Nodes.Compile(scope, context, "Negate");

            var lastResult = list.Last().Args2;

            list.Add(new Expression(26, lastResult, lastResult));
            return list;
        }
    }
}
