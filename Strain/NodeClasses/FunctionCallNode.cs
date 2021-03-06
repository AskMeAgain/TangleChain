﻿using System;
using System.Collections.Generic;
using System.Linq;
using Strain.Classes;
using TangleChainIXI.Smartcontracts;

namespace Strain.NodeClasses
{
    public class FunctionCallNode : Node
    {

        public string Name { get; set; }

        public FunctionCallNode(string name, List<Node> paraNodes)
        {
            Name = name;
            Nodes.AddRange(paraNodes);
        }

        public override List<Expression> Compile(Scope scope, ParserContext context)
        {

            var list = new List<Expression>();
            var paraList = scope.GetFunctionParameter(Name);

            try
            {
                for (int i = 0; i < Nodes.Count; i++)
                {

                    list.AddRange(Nodes[i].Compile(scope, context.NewContext()));

                    var last = list.Last().Args3;

                    list.Add(Factory.Copy(last, $"Parameters-{paraList[i]}-{Name}"));
                }
            }
            catch (Exception)
            {
                throw new Exception("provided Parameter number is not equal to the correct amount of parameters");
            }

            list.Add(Factory.JumpAndLink(Name));
            list.Add(Factory.Copy($"FunctionReturn-{Name}", context + "-Result"));

            return list;
        }
    }
}
