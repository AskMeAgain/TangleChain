﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StrainLanguage.Classes;
using TangleChainIXI.Smartcontracts;

namespace StrainLanguage.NodeClasses
{
    public class ReturnNode : ParserNode
    {

        public ReturnNode(ParserNode expParserNode)
        {
            Nodes.Add(expParserNode);
        }

        public override List<Expression> Compile(Scope scope, ParserContext context)
        {

            var funcName = scope.GetFunctionNameFromContext(context.ToString());

            var list = new List<Expression>();
            list.AddRange(Nodes.Compile(scope, context, "Return"));

            var lastResult = list.Last().Args2;
            list.Add(new Expression(00, lastResult, $"FunctionReturn-{funcName}"));

            return list;
        }
    }
}
