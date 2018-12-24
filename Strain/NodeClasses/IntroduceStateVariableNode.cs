﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StrainLanguage.Classes;
using TangleChainIXI.Smartcontracts;

namespace StrainLanguage.NodeClasses
{
    public class IntroduceStateVariableNode : Node
    {
        public string VariableName { get; protected set; }
        public int? MaxIndex { get; set; }

        public IntroduceStateVariableNode(string variableName, string maxIndex = null)
        {
            VariableName = variableName;

            if (int.TryParse(maxIndex, out int result))
            {
                MaxIndex = result - 1;
            }
        }

        public override List<Expression> Compile(Scope scope, ParserContext context)
        {

            context = context.OneContextUp();
            scope.AddVariable(VariableName, context.ToString());

            if (MaxIndex != null)
            {
                scope.ArrayIndex.Add(VariableName, MaxIndex.Value);
                var list = new List<Expression>();

                for (int i = 0; i <= MaxIndex; i++)
                {
                    scope.StateVariables.Add(VariableName + "_" + i);
                    list.Add(new Expression(10, VariableName + "_" + i, VariableName + "_" + i));
                }

                return list;
            }
            else
            {
                scope.StateVariables.Add(VariableName);
            }

            return new List<Expression>() {
                new Expression(10,VariableName,VariableName)
            };
        }
    }
}
