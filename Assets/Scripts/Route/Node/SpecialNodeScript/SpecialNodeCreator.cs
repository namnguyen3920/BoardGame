using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpecialNodeCreator : SpecialNodeFactory
{
    public override ISpecialNode CreateSpecialNode(NodeType nodeType)
    {
        switch(nodeType)
        {
            case NodeType.Bonus:
                return new Bonus();
            case NodeType.Fail:
                return new Fail();
            default:
                throw new ArgumentException("Invalid Type", "type");
        }
    }
}
