using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

// DA FARE: conversione espressioni inserimento parentesi esplicite

public static class Compiler
{
    public static Tuple<List<object>, List<object>> Parse(string program)
    {
        Parser array = null;
        Parser binaryOperation = null;
        Parser unaryOperation = null;
        Parser ternaryOperation = null;
        Parser cast = null;
        Parser structDefinition = null;
        Parser variable = null;
        Parser scope = null;
        Parser assignment = null;
        Parser functionCall = null;


        Parser code = Parser.Lazy(new Lazy<Parser>(() => scope));

        Parser value = Parser.Lazy(new Lazy<Parser>(() => Parser.Choice(new List<Parser>
            {
                Parser.ScalarValue(), Parser.String(), array
            })));

        array = Parser.Array(Parser.SequenceOf(new List<Parser>
            {
                Parser.Empty(true),
                value,
                Parser.Empty(true)
            }).Map(
            (result) => ((List<object>)result)[1],
            (indexes) => Parser.Extremes((List<object>)indexes)));

        Parser expression = Parser.Lazy(new Lazy<Parser>(() =>
            Parser.Choice(new List<Parser>
            {
                    Parser.SizeOf(), Parser.FunctionCall(functionCall), Parser.ScalarValue(),
                    unaryOperation, variable, binaryOperation, assignment, ternaryOperation, cast
            })));

        functionCall = Parser.SequenceOf(new List<Parser>
                            {
                                    Parser.Empty(true),
                                    Parser.Between(Parser.SequenceOf(new List<Parser>{Parser.Match("("), Parser.Empty(true)}),
                                    Parser.SeparetedBy(expression, Parser.SequenceOf(new List<Parser>
                                    {
                                        Parser.Empty(true), Parser.Match(","), Parser.Empty(true)
                                    }), true),
                                        Parser.SequenceOf(new List<Parser>{Parser.Empty(true), Parser.Match(")")}))
                            }).Map(
                            (result) => result,
                            (indexes) => ((List<object>)indexes)[1]);

        Parser expressionLine =
            Parser.SequenceOf(new List<Parser> { expression, Parser.Empty(true), Parser.Match(";") })
                .Map(
                    (result) => new List<object>((List<object>)result)[0],
                    (indexes) => Parser.Extremes((List<object>)indexes));

        cast = Parser.Between(Parser.SequenceOf(new List<Parser> { Parser.Match("("), Parser.Empty(true) }),
            Parser.SequenceOf(new List<Parser>
            {
                    Parser.Between(Parser.SequenceOf(new List<Parser> { Parser.Match("("), Parser.Empty(true) }),
                        Parser.SequenceOf(new List<Parser>
                        {
                            Parser.Type(),
                            Parser.Empty(true),
                            Parser.SeparetedBy(Parser.Match("*"), Parser.Empty(true), true).Map(
                                (result)=>((List<object>)result).Count,
                                (indexes)=>indexes)
                        }).Map(
                            (result)=>new List<object>{ ((List<object>)result)[0], ((List<object>)result)[2]},
                            (indexes)=>Parser.Extremes((List<object>)indexes)),
                        Parser.SequenceOf(new List<Parser> { Parser.Empty(true), Parser.Match(")") })),
                    Parser.Empty(true),
                    expression
            }), Parser.SequenceOf(new List<Parser> { Parser.Empty(true), Parser.Match(")") }))
            .Map(
                (result) => new List<object> { "cast", ((List<object>)result)[0], ((List<object>)result)[2] },
                (indexes) => Parser.Extremes((List<object>)indexes));

        variable = Parser.Choice(new List<Parser>
            {
                Parser.Between(
                    Parser.SequenceOf(new List<Parser>{Parser.Match("("), Parser.Empty(true)}),
                    Parser.SequenceOf(new List<Parser> { expression, Parser.Match("."), Parser.ExistingVariable() })
                        .Map((result)=>result, (indexes)=>Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>{Parser.Empty(true), Parser.Match(")")})
                ).Map(
                    (result) => new List<object> { "field", ((List<object>)result)[0], ((List<object>)result)[2] },
                    (indexes)=>indexes),
                Parser.Between(
                    Parser.SequenceOf(new List<Parser>{Parser.Match("("), Parser.Empty(true)}),
                    Parser.SequenceOf(new List<Parser> { expression, Parser.Match("->"), Parser.ExistingVariable() })
                        .Map((result)=>result, (indexes) => Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>{Parser.Empty(true), Parser.Match(")")})
                ).Map(
                    (result) => new List<object> { "pointed", ((List<object>)result)[0], ((List<object>)result)[2] },
                    (indexes)=>indexes),
                Parser.Between(
                    Parser.SequenceOf(new List<Parser>{Parser.Match("("), Parser.Empty(true)}),
                    Parser.SequenceOf(new List<Parser>
                        { expression, Parser.Between(Parser.SequenceOf(new List<Parser>{Parser.Match("["), Parser.Empty(true)}), expression, Parser.SequenceOf(new List<Parser>{Parser.Empty(true), Parser.Match("]")})) })
                        .Map((result)=>result, (indexes)=>Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>{Parser.Empty(true), Parser.Match(")")})
                ).Map(
                    (result) => new List<object> { "index", ((List<object>)result)[0], ((List<object>)result)[1] },
                    (indexes) => indexes),
                Parser.ExistingVariable()
            });

        binaryOperation = Parser.Between(Parser.SequenceOf(new List<Parser> { Parser.Match("("), Parser.Empty(true) }),
            Parser.SequenceOf(new List<Parser>
            {
                    expression,
                    Parser.Empty(true),
                    Parser.BinaryOperator(),
                    Parser.Empty(true),
                    expression
            }).Map((result) => result, (indexes) => Parser.Extremes((List<object>)indexes)),
            Parser.SequenceOf(new List<Parser> { Parser.Empty(true), Parser.Match(")") })).Map(
                (result) =>
                    new List<object> { ((List<object>)result)[2], ((List<object>)result)[0], ((List<object>)result)[4] },
                (indexes) => indexes);

        unaryOperation = Parser.Between(Parser.SequenceOf(new List<Parser> { Parser.Match("("), Parser.Empty(true) }),
            Parser.Choice(new List<Parser>
            {
                    Parser.SequenceOf(new List<Parser>{Parser.Match("*"), variable}).Map(
                        (result)=>new List<object> { "*", ((List<object>)result)[1]},
                        (indexes) => Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>{Parser.Match("&"), variable}).Map(
                        (result)=>new List<object> { "&", ((List<object>)result)[1]},
                        (indexes) => Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>{Parser.Match("!"), variable}).Map(
                        (result)=>new List<object> { "!", ((List<object>)result)[1]},
                        (indexes) => Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser> { Parser.UnaryOperator(), variable}).Map(
                        (result)=>new List<object> { (string)((List<object>)result)[0] + "pre", ((List<object>)result)[1]},
                        (indexes) => Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser> {variable, Parser.UnaryOperator() }).Map(
                        (result)=>new List<object> { (string)((List<object>)result)[1] + "post", ((List<object>)result)[0]},
                        (indexes) => Parser.Extremes((List<object>)indexes))
            }), Parser.SequenceOf(new List<Parser> { Parser.Empty(true), Parser.Match(")") }));

        ternaryOperation = Parser.Between(Parser.SequenceOf(new List<Parser> { Parser.Match("("), Parser.Empty(true) }), Parser.SequenceOf(new List<Parser>
            {
                expression,
                Parser.Empty(true),
                Parser.Match("?"),
                Parser.Empty(true),
                expression,
                Parser.Empty(true),
                Parser.Match(":"),
                Parser.Empty(true),
                expression
            }), Parser.SequenceOf(new List<Parser> { Parser.Empty(true), Parser.Match(")") })).Map(
            (result) => new List<object>
            { "?", ((List<object>)result)[0], ((List<object>)result)[4], ((List<object>)result)[8]},
            (indexes) => Parser.Extremes((List<object>)indexes));

        assignment = Parser.SequenceOf(new List<Parser>
            {
                variable,
                Parser.Empty(true),
                Parser.Match("="),
                Parser.Empty(true),
                Parser.Choice(new List<Parser> { array, Parser.String(), expression })
            }).Map(
            (result) => new List<object> { ((List<object>)result)[0], ((List<object>)result)[4] },
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser declarationAssignment = Parser.SequenceOf(new List<Parser>
            {
                Parser.Variable(),
                Parser.Empty(true),
                Parser.Match("="),
                Parser.Empty(true),
                Parser.Choice(new List<Parser> { array, Parser.String(), expression })
            }).Map(
            (result) => new List<object> { ((List<object>)result)[0], ((List<object>)result)[4] },
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser specialAssignment = Parser.SequenceOf(new List<Parser>
            {
                variable,
                Parser.Empty(true),
                Parser.AssigmentsSigns(),
                Parser.Empty(true),
                expression
            }).Map((
                result) =>
            {
                string op = ((string)((List<object>)result)[2]).Substring(0,
                    ((string)((List<object>)result)[2]).IndexOf("="));
                return new List<object>
                    {
                        ((List<object>)result)[0], new List<object>
                        {
                            op, ((List<object>)result)[0], ((List<object>)result)[4]
                        }
                    };
            },
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser asssigmentLine = Parser.SequenceOf(new List<Parser>
            {
                Parser.Choice(new List<Parser> { assignment, specialAssignment }),
                Parser.Empty(true),
                Parser.Match(";")
            }).Map(
            (result) => new List<object> { "assignment", ((List<object>)((List<object>)result)[0])[0], ((List<object>)((List<object>)result)[0])[1] },
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser variableDeclaration = Parser.Lazy(new Lazy<Parser>(() => Parser.SequenceOf(new List<Parser>
            {
                Parser.Empty(true),
                Parser.Choice(new List<Parser>
                {
                    Parser.Enum().Map(
                        (result) => new List<object>{"definition", result},
                        (indexes) => indexes),
                    structDefinition.Map(
                        (result) => new List<object>{"definition", result},
                        (indexes)=> indexes),
                    Parser.Type()
                }),
                Parser.Empty(true),
                Parser.Many(Parser.SequenceOf(new List<Parser>
                {
                    Parser.SeparetedBy(Parser.Match("*"), Parser.Empty(true), true)
                        .Map((result) => ((List<object>)result).Count, (indexes) => indexes),
                    Parser.Choice(new List<Parser>
                    {
                        declarationAssignment,
                        Parser.Variable().Map(
                            (result) => new List<object> { result, new List<object> { "void" } },
                            (indexes) => indexes)
                    }),
                    Parser.Empty(true),
                    Parser.Optional(Parser.Match(",")),
                    Parser.Empty(true)
                }).Map(
                    (result) =>
                    {
                        ((List<object>)((List<object>)result)[1]).Insert(1, ((List<object>)result)[0]);
                        return ((List<object>)result)[1];
                    },
                    (indexes)=> indexes)),
                Parser.Match(";")
            }).Map(
            (result) =>
            {
                foreach (List<object> o in (List<object>)((List<object>)result)[3])
                    Parser.variables.Add((string)o[0]);
                if ((((List<object>)((List<object>)result)[1])[0]).Equals("definition"))
                    return new List<object>
                        {
                                ((List<object>)((List<object>)result)[1])[1],
                                new List<object> { "declarations", ((List<object>)((List<object>)((List<object>)result)[1])[1])[0], ((List<object>)result)[3] }
                        };
                return new List<object> { "declarations", ((List<object>)result)[1], ((List<object>)result)[3] };
            },
            (indexes) =>
            {
                ((List<object>)indexes).RemoveAt(0);
                return Parser.Extremes((List<object>)indexes);
            })));

        structDefinition = Parser.SequenceOf(new List<Parser>
            {
                Parser.Choice(new List<Parser>{Parser.Match("struct"), Parser.Match("union")}),
                Parser.Optional(Parser.SequenceOf(new List<Parser>{Parser.Empty(), Parser.Variable() }).Map(
                    (result)=>result, (indexes)=>indexes))
                    .Map(
                        (result) =>
                        {
                            if (result is string) return "defaultName"; // aggiungere un indice per differnziarli
                            return ((List<object>)result)[1];
                        },
                        (indexes)=> indexes),
                Parser.Empty(true), Parser.Between(Parser.Match("{"),
                    Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Empty(true),
                        Parser.Many(variableDeclaration.Chain((result) =>
                        {
                            if (((string)((List<object>)((List<object>)((List<object>)((List<object>)result)[2])[0])[2])[0]).Equals("void"))
                                return Parser.Success().Map(
                                    (r) =>
                                    {
                                        ((List<object>)r).RemoveAt(0);
                                        foreach (List<object> o in (List<object>)((List<object>)r)[1])
                                            o.RemoveAt(2);
                                        return r;
                                    },
                                    (indexes)=>indexes);
                            return Parser.Error();
                        })),
                        Parser.Empty(true)
                    }).Map(
                        (result)=> ((List<object>)result)[1],
                        (indexes)=> Parser.Extremes((List<object>)((List<object>)indexes)[1])),
                    Parser.Match("}"))
            }).Map(
            (result) => new List<object>
            {
                    new List<object> { ((List<object>)result)[0], ((List<object>)result)[1] }, ((List<object>)result)[3]
            },
            (indexes) => Parser.Extremes((List<object>)indexes)
        );

        Parser emptyLines = Parser.Many(Parser.Choice(new List<Parser>
            {
                Parser.InLineComment(), Parser.ManyLinesComment(), Parser.Empty()
            }), true);

        Parser returnLine = Parser.SequenceOf(new List<Parser>
            {
                Parser.Match("return"),
                Parser.Optional(Parser.SequenceOf(new List<Parser>
                {
                    Parser.Empty(),
                    expression
                }).Map((result)=>((List<object>)result)[1], (indexes)=>indexes))
                    .Map((result)=>result is string ? new List<object>{result} : result, (indexes)=>indexes),
                Parser.Empty(true),
                Parser.Match(";")
            }).Map(
            (result) => new List<object> { "return", ((List<object>)result)[1] },
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser structDefinitionLine = Parser.SequenceOf(new List<Parser>
            {
                structDefinition, Parser.Empty(true), Parser.Match(";")
            }).Map(
            (result) => ((List<object>)result)[0],
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser condition = Parser.Lazy(new Lazy<Parser>(() => Parser.SequenceOf(new List<Parser>
            {
                Parser.Match("if"),
                Parser.Empty(true),
                Parser.Between(Parser.Match("("), expression, Parser.Match(")")),
                Parser.Empty(true),
                scope.Map(
                    (result) => ((string)((List<object>)result)[0]).Equals("scope") ? ((List<object>)result)[1] : new List<object> { result },
                    (indexes) => indexes is Tuple<int, int>? new List<object>{indexes } : ((List<object>)indexes)[1]),
                Parser.Empty(true),
                Parser.Optional(Parser.SequenceOf(new List<Parser>
                {
                    Parser.Match("else"),
                    Parser.Empty(true),
                    scope.Map(
                    (result) => ((string)((List<object>)result)[0]).Equals("scope") ? ((List<object>)result)[1] : new List<object> { result },
                    (indexes) => indexes is Tuple<int, int>? new List<object>{indexes } : ((List<object>)indexes)[1])
                }))
            }))).Map(
            (result) =>
            {
                List<object> r = new List<object> { "if", ((List<object>)result)[2], ((List<object>)result)[4] };
                if (((List<object>)result)[6] is string)
                    r.Add("end");
                else
                {
                    r.Add("else");
                    r.Add(((List<object>)((List<object>)result)[6])[2]);
                }
                return r;
            },
            (indexes) =>
            {
                List<object> r = new List<object> { "if", Parser.Extremes(new List<object> { ((List<object>)indexes)[0], ((List<object>)indexes)[2] }), ((List<object>)indexes)[4] };
                if (!(((List<object>)indexes)[6] is string))
                {
                    r.Add("else");
                    r.Add(((List<object>)((List<object>)indexes)[6])[2]);
                }
                return r;
            });

        Parser whileCycle = Parser.Lazy(new Lazy<Parser>(() => Parser.Choice(new List<Parser>
            {
                Parser.SequenceOf(new List<Parser>
                {
                    Parser.Match("while"),
                    Parser.Empty(true),
                    Parser.Between(Parser.Match("("), expression, Parser.Match(")")),
                    Parser.Empty(true),
                    scope.Map(
                        (result) => ((string)((List<object>)result)[0]).Equals("scope") ? ((List<object>)result)[1] : new List<object> { result },
                        (indexes) => indexes is Tuple<int, int>? new List<object>{indexes } : ((List<object>)indexes)[1])
                }).Map(
                    (result) =>
                    {
                        ((List<object>)((List<object>)result)[4]).Add(new List<object>{"endOfIteration"});
                        ((List<object>)((List<object>)result)[4]).Add(new List<object>{"endOfIteration"});
                        return new List<object> { "while", ((List<object>)result)[2], ((List<object>)result)[4] };
                    },
                    (indexes)=> new List<object>
                    {
                        "while", Parser.Extremes(new List<object>{((List<object>)indexes)[0], ((List<object>)indexes)[2]}), ((List<object>)indexes)[4]
                    }),
                Parser.SequenceOf(new List<Parser>
                {
                    Parser.Match("do"),
                    Parser.Empty(true),
                    scope.Map(
                        (result) => ((string)((List<object>)result)[0]).Equals("scope") ? ((List<object>)result)[1] : new List<object> { result },
                        (indexes) => indexes is Tuple<int, int>? new List<object>{indexes } : ((List<object>)indexes)[1]),
                    Parser.Empty(true),
                    Parser.Match("while"),
                    Parser.Empty(true),
                    Parser.Between(Parser.Match("("), expression, Parser.Match(")")),
                    Parser.Empty(true),
                    Parser.Match(";")
                }).Map(
                    (result) =>
                        new List<object>
                        {
                            "twoInstructions",
                            new List<object>{"scope", ((List<object>)result)[2]},
                            new List<object> { "while", ((List<object>)result)[6], new List<object>((List<object>)((List<object>)result)[2]) { new List<object> { "endOfIteration" }, new List<object> { "endOfIteration" } } }
                        },
                    (indexes)=> new List<object>
                    {
                        "twoInstructions",
                        new List<object>{"scope", ((List<object>)indexes)[2]},
                        new List<object>{"while", Parser.Extremes(new List<object>{((List<object>)indexes)[4], ((List<object>)indexes)[6]}), ((List<object>)indexes)[2]}
                    }),
            })));

        Parser declarationFor = Parser.SequenceOf(new List<Parser>
            {
                Parser.Empty(true),
                Parser.Type(),
                Parser.Empty(true),
                Parser.SeparetedBy(Parser.Match("*"), Parser.Empty(true), true)
                    .Map((result) => ((List<object>)result).Count, (indexes)=>indexes),
                Parser.Choice(new List<Parser>
                {
                    declarationAssignment,
                    Parser.Variable().Map(
                        (result) => new List<object> { result, new List<object> { "void" }},
                        (indexes)=>indexes),
                }),
                Parser.Empty(true)
            }).Map(
            (result) =>
            {
                Parser.variables.Add((string)((List<object>)((List<object>)result)[4])[0]);
                return new List<object>
                        { "declarations", ((List<object>)result)[1], new List<object>{ new List<object> { ((List<object>)((List<object>)result)[4])[0], ((List<object>)result)[3], ((List<object>)((List<object>)result)[4])[1] } }};
            },
            (indexes) =>
            {
                ((List<object>)indexes).RemoveAt(0);
                ((List<object>)indexes).RemoveAt(((List<object>)indexes).Count - 1);
                return Parser.Extremes((List<object>)indexes);
            });

        Parser assigmentFor = Parser.SequenceOf(new List<Parser>
            {
                Parser.Choice(new List<Parser> { assignment, specialAssignment }),
                Parser.Empty(true)
            }).Map(
                (result) => new List<object> { "assignment", ((List<object>)((List<object>)result)[0])[0], ((List<object>)((List<object>)result)[0])[1] },
                (indexes) => ((List<object>)indexes)[0]);


        Parser forCode = Parser.SequenceOf(new List<Parser>
            {
                Parser.Many(Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Choice(new List<Parser>
                        {
                            declarationFor,
                            assigmentFor,
                            expression
                        }),
                        Parser.Empty(true),
                        Parser.Match(","),
                        Parser.Empty(true)
                    }).Map((result) => ((List<object>)result)[0],
                        (indexes) =>
                        {
                            ((List<object>)indexes).RemoveAt(((List<object>)indexes).Count - 1);
                            return Parser.Extremes((List<object>)indexes);
                        }),
                    true),
                Parser.Choice(new List<Parser>
                {
                    declarationFor,
                    assigmentFor,
                    expression
                }),
                Parser.Empty(true)
            }).Map(
            (result) =>
            {
                List<object> r = new List<object>();
                foreach (List<object> o in (List<object>)((List<object>)result)[0]) r.Add(o);
                r.Add(((List<object>)result)[1]);
                return r;
            },
            (indexes) =>
            {
                ((List<object>)indexes).RemoveAt(((List<object>)indexes).Count - 1);
                if (((List<object>)((List<object>)indexes)[0]).Count == 0) ((List<object>)indexes).RemoveAt(0);
                else ((List<object>)indexes)[0] = Parser.Extremes((List<object>)((List<object>)indexes)[0]);
                return Parser.Extremes((List<object>)indexes);
            });


        Parser forCycle = Parser.Lazy(new Lazy<Parser>(() => Parser.SequenceOf(new List<Parser>
            {
                Parser.Match("for"),
                Parser.Empty(true),
                Parser.Between(Parser.Match("("), Parser.SequenceOf(new List<Parser>
                {
                    Parser.Empty(true), forCode, Parser.Empty(true), Parser.Match(";"),
                    Parser.Empty(true), expression, Parser.Empty(true), Parser.Match(";"),
                    Parser.Empty(true), forCode, Parser.Empty(true)
                }), Parser.Match(")"), true).Map(
                    (result) => new List<object>
                        {((List<object>)result)[1], ((List<object>)result)[5], ((List<object>)result)[9]},
                    (indexes)=>new List<object>
                        {((List<object>)indexes)[1], ((List<object>)indexes)[5], ((List<object>)indexes)[9]}),
                Parser.Empty(true),
                scope.Map(
                    (result) => ((string)((List<object>)result)[0]).Equals("scope") ? ((List<object>)result)[1] : new List<object> { result },
                    (indexes) => indexes is Tuple<int, int>? new List<object>{indexes } : ((List<object>)indexes)[1])
            }))).Map(
            (result) =>
            {
                List<object> r = new List<object> { "for" };
                foreach (List<object> o in (List<object>)((List<object>)result)[2]) r.Add(o);
                r.Add((List<object>)((List<object>)result)[4]);
                return r;
            },
            (indexes) => new List<object>
                {
                    ((List<object>)indexes)[0],
                    new List<object> { ((List<object>)indexes)[1], ((List<object>)indexes)[2], ((List<object>)indexes)[3] },
                    ((List<object>)indexes)[4]
                }
            ).Map(
                (result) =>
                {
                    List<object> r = new List<object>(((List<object>)((List<object>)result)[4]));
                    r.AddRange((List<object>)((List<object>)result)[3]);
                    r.Add(new List<object> { "endOfIteration" });
                    return new List<object>{"scope", new List<object>((List<object>)((List<object>)result)[1])
                        {new List<object>{"while", new List<object>((List<object>) ((List<object>)result)[2]), r } } };
                },
                (indexes) =>
                {
                    List<object> r = new List<object>((List<object>)((List<object>)indexes)[2])
                        { ((List<object>)((List<object>)((List<object>)indexes)[1])[1])[2] };
                    return new List<object>
                    {
                        "scope",
                        new List<object>
                        {
                            ((List<object>)((List<object>)((List<object>)indexes)[1])[1])[0],
                            new List<object>
                                { "while", ((List<object>)((List<object>)((List<object>)indexes)[1])[1])[1], r }
                        }
                    };
                });

        Parser switchStatement = Parser.Lazy(new Lazy<Parser>(() => Parser.SequenceOf(new List<Parser>
            {
                Parser.Match("switch"),
                Parser.Empty(true),
                Parser.Between(Parser.Match("("), expression, Parser.Match(")")),
                Parser.Empty(true),
                Parser.Between(Parser.Match("{"), Parser.SequenceOf(new List<Parser>
                {
                    Parser.Many(Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Empty(true),
                        Parser.Match("case"),
                        Parser.Empty(),
                        expression,
                        Parser.Empty(true),
                        Parser.Match(":"),
                        Parser.Empty(true),
                        Parser.Many(code, true)
                    }).Map(
                            (result) => new List<object> {new List<object>{"case", ((List<object>)result)[3]}, ((List<object>)result)[7]},
                            (indexes)=>new List<object>{ new List<object>{"case", ((List<object>)indexes)[3]}, ((List<object>)indexes)[7]}),
                        true),
                    Parser.Optional(Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Empty(true),
                        Parser.Match("default"),
                        Parser.Empty(true),
                        Parser.Match(":"),
                        Parser.Empty(true),
                        Parser.Many(code, true)
                    }).Map(
                        (result) => new List<object> {new List<object>{"default"}, ((List<object>)result)[5] },
                        (indexes)=> new List<object>{new List<object>{"default"}, ((List<object>)indexes)[5]}))
                }), Parser.Match("}"), true).Map((result) =>
                {
                    List<object> r = new List<object>();
                    foreach (List<object> o in (List<object>)((List<object>)result)[0])
                    {
                        r.Add(o[0]);
                        r.Add(new List<object>((List<object>)o[1]){new List<object>{"switchContinue"}});
                    }

                    if (!(((List<object>)result)[1] is string))
                    {
                        r.Add(((List<object>)((List<object>)result)[1])[0]);
                        r.Add(((List<object>)((List<object>)result)[1])[1]);
                    }
                    return r;
                }, (indexes) =>
                {
                    List<object> r = new List<object>();
                    foreach (List<object> o in (List<object>)((List<object>)indexes)[0])
                    {
                        r.Add(o[0]);
                        r.Add(new List<object>((List<object>)o[1]){new List<object>{"switchContinue"}});
                    }

                    if (!(((List<object>)indexes)[1] is string))
                    {
                        r.Add(((List<object>)((List<object>)indexes)[1])[0]);
                        r.Add(((List<object>)((List<object>)indexes)[1])[1]);
                    }
                    return r;
                })
            }))).Map(
                (result) =>
                {
                    List<object> r = new List<object> { "switch", ((List<object>)result)[2] };
                    foreach (object o in (List<object>)((List<object>)result)[4])
                        r.Add(o);
                    return r;
                },
                (indexes) =>
                {
                    List<object> r = new List<object> { "switch", Parser.Extremes(new List<object> { ((List<object>)indexes)[0], ((List<object>)indexes)[2] }) };
                    foreach (object o in (List<object>)((List<object>)indexes)[4])
                        r.Add(o);
                    return r;
                });
        Parser functionDeclaration = Parser.SequenceOf(new List<Parser>
            {
                Parser.Type(), Parser.Empty(true), Parser.Variable(),
                Parser.Empty(true), Parser.Between(Parser.Match("("), Parser.SeparetedBy(
                        Parser.SequenceOf(new List<Parser>
                        {
                            Parser.Empty(true),
                            Parser.Type(),
                            Parser.Empty(true),
                            Parser.SequenceOf(new List<Parser>
                            {
                                Parser.SeparetedBy(Parser.Match("*"), Parser.Empty(true), true)
                                    .Map((result) => ((List<object>)result).Count, (indexes)=>indexes),
                                Parser.Variable(),
                                Parser.Empty(true),
                            }).Map(
                                (result) => new List<object>{ ((List<object>)result)[0], ((List<object>)result)[1]},
                                (indexes)=>((List<object>)indexes)[1])
                        }).Map(
                            (result) => new List<object> {((List<object>)result)[1], ((List<object>)result)[3] },
                            (indexes)=>
                            {
                                ((List<object>)indexes).RemoveAt(0);
                                return Parser.Extremes((List<object>)indexes);
                            }),
                    Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Empty(true), Parser.Match(","), Parser.Empty(true)
                    }),true), Parser.Match(")"))
            }).Map(
            (result) =>
            {
                Parser.functions.Add((string)((List<object>)result)[2]);
                return new List<object>
                    { ((List<object>)result)[0], ((List<object>)result)[2], ((List<object>)result)[4] };
            },
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser functionPrototype = Parser.SequenceOf(new List<Parser> { functionDeclaration, Parser.Empty(true), Parser.Match(";") }).Map(
                (result) => new List<object> { "functionPrototype", ((List<object>)result)[0] },
                (indexes) => Parser.Extremes((List<object>)indexes));


        Parser functionDefinition = Parser.SequenceOf(new List<Parser>
            {
                functionDeclaration.Map(
                    (result) =>
                    {
                        foreach (List<object> o in (List<object>)((List<object>)result)[2])
                            Parser.variables.Add((string)((List<object>)o[1])[1]);
                        return result;
                    },
                    (indexes)=>indexes),
                Parser.Empty(true),
                Parser.Between(Parser.Match("{"), Parser.Many(code, true).Map(
                    (result)=>
                    {
                        ((List<object>)result).Add(new List<object>{"return", new List<object>{"void"}});
                        return result;
                    },
                    (indexes)=>indexes),
                    Parser.Match("}"), true)
            }).Map(
            (result) => new List<object> { "functionDefinition", ((List<object>)((List<object>)result)[0])[1], ((List<object>)result)[2], ((List<object>)result)[0]},
            (indexes) =>  new List<object>{"functionDefinition", "name", ((List<object>)indexes)[2]});

        Parser typeDefinition = Parser.SequenceOf(new List<Parser>
            {
                Parser.Match("typedef"),
                Parser.Empty(),
                Parser.Choice(new List<Parser>
                {
                    Parser.SequenceOf(new List<Parser>
                    {
                        Parser.SeparetedBy(Parser.TypeWord(), Parser.Empty()).Map((result)=>result, (indexes)=>Parser.Extremes((List<object>)indexes)), Parser.Choice(new List<Parser>
                        {
                            Parser.Many(Parser.SequenceOf(new List<Parser>
                            {
                                Parser.Empty(true), Parser.Match("*"), Parser.Empty(true)
                            })).Map((result) => ((List<object>)result).Count, (indexes)=>indexes),
                            Parser.LastEmpty().Map((result) => 0, (indexes)=>indexes)
                        }),
                        Parser.Variable(), Parser.Empty(true),
                        Parser.Match(";")
                    }).Map(
                        (result) =>
                        {
                            Parser.types.Add((string)((List<object>)result)[2]);
                            return new List<object>
                            {
                                "typedef", ((List<object>)result)[0], ((List<object>)result)[1], ((List<object>)result)[2]
                            };
                        },
                        (indexes)=>Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Choice(new List<Parser>{Parser.Match("struct"), Parser.Match("union"), Parser.Match("enum")}),
                        Parser.Empty(),
                        Parser.Variable(),
                        Parser.Choice(new List<Parser>
                        {
                            Parser.Many(Parser.SequenceOf(new List<Parser>
                            {
                                Parser.Empty(true), Parser.Match("*"), Parser.Empty(true)
                            })).Map((result) => ((List<object>)result).Count, (indexes)=>indexes),
                            Parser.Empty().Map((result) => 0, (indexes)=>indexes)
                        }),
                        Parser.Variable(),
                        Parser.Empty(true),
                        Parser.Match(";")
                    }).Map(
                        (result) =>
                        {
                            Parser.types.Add((string)((List<object>)result)[4]);
                            return new List<object>
                            {
                                "typedef",
                                new List<object>
                                    { ((List<object>)result)[0], ((List<object>)result)[2] },
                                ((List<object>)result)[3],
                                ((List<object>)result)[4]
                            };
                        },
                        (indexes)=>Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>
                    {
                        structDefinition, Parser.Many(Parser.SequenceOf(new List<Parser>
                        {
                            Parser.Empty(true), Parser.Match("*"), Parser.Empty(true)
                        }), true).Map((result) => ((List<object>)result).Count, (indexes)=>indexes),
                        Parser.Empty(true),
                        Parser.Variable(), Parser.Empty(true),
                        Parser.Match(";")
                    }).Map(
                        (result) =>
                        {
                            Parser.types.Add((string)((List<object>)result)[3]);
                            return new List<object>
                            {
                                "doubleInstruction",
                                (List<object>)((List<object>)result)[0],
                                new List<object>
                                {
                                    "typedef", ((List<object>)((List<object>)result)[0])[0], ((List<object>)result)[1],
                                    ((List<object>)result)[3]
                                }
                            };
                        },
                        (indexes) => Parser.Extremes((List<object>)indexes)),
                    Parser.SequenceOf(new List<Parser>
                    {
                        Parser.Enum(), Parser.Many(Parser.SequenceOf(new List<Parser>
                        {
                            Parser.Empty(true), Parser.Match("*"), Parser.Empty(true)
                        }), true).Map((result) => ((List<object>)result).Count, (indexes)=>indexes),
                        Parser.Empty(true),
                        Parser.Variable().Map(
                            (result) => ((List<object>)result)[1],
                            (indexes)=>indexes),
                        Parser.Empty(true),
                        Parser.Match(";")
                    }).Map(
                        (result) =>
                        {
                            Parser.types.Add((string)((List<object>)result)[3]);
                            return new List<object>
                            {
                                (List<object>)((List<object>)result)[0],
                                new List<object>
                                {
                                    "typedef", ((List<object>)((List<object>)result)[0])[0], ((List<object>)result)[1],
                                    ((List<object>)result)[3]
                                }
                            };
                        },
                        (indexes)=>Parser.Extremes((List<object>)indexes))
                })
            }).Map(
            (result) => ((List<object>)result)[2],
            (indexes) => Parser.Extremes((List<object>)indexes));

        Parser line = Parser.Choice(new List<Parser>
            {
                Parser.Match(";").Map(
                    (result) => new List<object> { "void" },
                    (indexes) => indexes),
                Parser.Break(), Parser.Continue(), Parser.EnumLine(), Parser.Goto(),
                typeDefinition, structDefinitionLine, returnLine, variableDeclaration, asssigmentLine, expressionLine, functionPrototype
            });

        scope = Parser.SequenceOf(new List<Parser>
            {
                emptyLines,
                Parser.Optional(Parser.Label()),
                emptyLines,
                Parser.Choice(new List<Parser>
                {
                    condition,
                    whileCycle,
                    forCycle,
                    switchStatement,
                    functionDefinition,
                    Parser.Between(Parser.SequenceOf(new List<Parser>{Parser.Match("{"), Parser.Empty(true)}),
                        Parser.Many(code, true).Map(
                            (result)=>((List<object>)result).Count == 0 ? new List<object>{new List<object>{"void"}} : result,
                            (indexes)=>((List<object>)indexes).Count == 0 ? new List<object>{new List<object>{"void"}}: indexes),
                    Parser.SequenceOf(new List<Parser>{Parser.Empty(true), Parser.Match("}")}), true)
                        .Map(
                            (result)=> new List<object> { "scope", result },
                            (indexes) => new List<object>{ "scope", indexes }),
                    line
                }),
                emptyLines
            }).Map(
            (result) =>
            {
                if (((List<object>)result)[1] is string) return ((List<object>)result)[3];
                return new List<object> { "label", ((List<object>)((List<object>)result)[1])[1], ((List<object>)result)[3] };
            },
            (indexes) =>
            {
                if (((List<object>)indexes)[1] is string) return ((List<object>)indexes)[3];
                return new List<object> { "label", ((List<object>)((List<object>)indexes)[1])[1], ((List<object>)indexes)[3] };
            });

        Parser programParser = Parser.Many(code, true);

        ParserState res = programParser.Run(new ParserState(program));
        Debug.Log(res.Result());
        Debug.Log(res.Indexes());
        return new Tuple<List<object>, List<object>>((List<object>)res.result, (List<object>)res.indexes);
    }

    public static class ReservedKeyWords


    {
        private static string[] keyWords =
        {
            "auto", "break", "case", "char", "const",
            "continue", "default", "do", "double", "else",
            "enum", "extern", "float", "for", "goto", "if",
            "int", "long", "register", "return", "short", "signed",
            "sizeof", "static", "struct", "switch", "typedef",
            "union", "unsigned", "void", "volatile", "while"
        };

        public static bool CheckString(string a)
        {
            foreach (string s in keyWords)
                if (a.StartsWith(s))
                    return true;
            return false;
        }
    }

    public static class RegexStrings
    {
        public static string letters = "^[A-Za-z]+";
        public static string lettersOrNone = "^[A-Za-z]*";
        public static string digits = "^\\d+";
        public static string digitsOrNone = "^\\d*";
        public static string variableName = "^[A-Za-z_][A-Za-z_\\d]*";
    }

    public class Parser
    {
        public static List<string> variables = new List<string>();
        public static List<string> types = new List<string>();
        public static List<string> functions = new List<string>();


        private Func<ParserState, ParserState> transformFunction;

        public ParserState Run(ParserState parserState) => transformFunction(parserState);

        public Parser(Func<ParserState, ParserState> fn) => transformFunction = fn;

        public Parser Map(Func<object, object> fn1, Func<object, object> fn2)
        {
            return new Parser(
                (parserState) =>
                {
                    ParserState nextState = Run(parserState);
                    if (nextState.isError) return nextState;
                    return new ParserState(nextState.index, nextState.targetString, fn1(nextState.result), fn2(nextState.indexes));
                });
        }

        public static Parser Between(Parser around, Parser content, bool seeInsideIndexes = false) => Between(around, content, around, seeInsideIndexes);

        public static Parser Between(Parser left, Parser content, Parser right, bool seeInsideIndexes = false)
        {
            return new Parser((parserState) =>
            {
                int startIndex = parserState.index;
                if (parserState.isError) return parserState;
                Parser parser = SequenceOf(new List<Parser>() { left, content, right }).Map(
                    (results) => ((List<object>)results)[1],
                    (indexes) => ((List<object>)indexes)[1]);
                ParserState finalState = parser.Run(parserState);
                if (seeInsideIndexes) return finalState;
                finalState.indexes = new Tuple<int, int>(startIndex, finalState.index);
                return finalState;
            });
        }

        public Parser ErrorMap(Func<object, object> fn)
        {
            return new Parser(
                (parserState) =>
                {
                    ParserState nextState = this.Run(parserState);
                    if (!nextState.isError) return nextState;
                    return new ParserState(true, (string)fn(nextState.result));
                });
        }

        public Parser Chain(Func<object, Parser> fn)
        {
            return new Parser(
                (parserState) =>
                {
                    ParserState nextState = Run(parserState);
                    if (nextState.isError) return nextState;
                    Parser nextParser = fn(nextState.result);
                    return nextParser.Run(nextState);
                });
        }

        public static Parser Any(int count = 1)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = 0;
                    if (parserState.isError) return parserState;
                    if (parserState.index + count > parserState.targetString.Length)
                        return new ParserState(true, "unexpected end of string");
                    return new ParserState(parserState.index + count, parserState.targetString,
                        parserState.targetString.Substring(parserState.index, count), new Tuple<int, int>(startIndex, parserState.index + count));
                });
        }

        public static Parser Empty(bool allowNone = false) =>
            Many(Choice(new List<Parser>() { Match(" "), Match("\t"), Match("\n") }), allowNone).Map(
                (result) => " ",
                (indexes) => indexes);

        public static Parser Optional(Parser parser)
        {
            return new Parser((parserState) =>
            {
                int startIndex = parserState.index;
                ParserState nextState = parser.Run(parserState);
                if (nextState.isError)
                    return new ParserState(parserState.index, parserState.targetString, "void", "void");
                return nextState;
            });
        }
        public static Parser Variable()
        {
            return new Parser((parserState) =>
            {
                int startIndex = parserState.index;
                if (parserState.isError) return parserState;
                string checkString = parserState.targetString.Substring(parserState.index);
                if (ReservedKeyWords.CheckString(checkString))
                    return new ParserState(true, "used a reserved name for a variable name");
                Match m = Regex.Match(checkString, RegexStrings.variableName);
                if (m.Success)
                    return new ParserState(parserState.index + m.Length, parserState.targetString, m.Value, new Tuple<int, int>(startIndex, parserState.index + m.Length));
                return new ParserState(true, $"expecting a letter at index {parserState.index}, got {checkString[0]}");
            });
        }

        public static Parser ExistingVariable()
        {
            return new Parser((parserState) =>
            {
                int startIndex = parserState.index;
                if (parserState.isError) return parserState;
                string checkString = parserState.targetString.Substring(parserState.index);
                if (ReservedKeyWords.CheckString(checkString))
                    return new ParserState(true, "used a reserved name for a variable name");

                foreach (string s in variables)
                    if (checkString.StartsWith(s))
                    {
                        Match m = Regex.Match(checkString.Substring(s.Length, 1), "[\\da-zA-Z_]");
                        if (m.Success) break;
                        return new ParserState(parserState.index + s.Length, parserState.targetString,
                            new List<object> { "variable", s }, new Tuple<int, int>(startIndex, parserState.index + s.Length));
                    }
                return new ParserState(true, $"expecting a letter at index {parserState.index}, got {checkString[0]}");
            });
        }

        public static Parser End(int numOfCharToCheck = 1)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = parserState.index;
                    if (parserState.isError) return parserState;
                    if (parserState.index + numOfCharToCheck >= parserState.targetString.Length)
                        return new ParserState(parserState.targetString.Length, parserState.targetString, "end", new Tuple<int, int>(startIndex, parserState.targetString.Length));
                    return new ParserState(true, "string didn't ended as expected");
                });
        }

        public static Parser RegexMatch(string s)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = parserState.index;
                    if (parserState.isError) return parserState;
                    if (!s.StartsWith("^")) s = "^" + s;
                    string checkString = parserState.targetString.Substring(parserState.index);
                    Match m = Regex.Match(checkString, s);
                    if (m.Success)
                        return new ParserState(parserState.index + m.Length, parserState.targetString, m.Value, new Tuple<int, int>(startIndex, parserState.index + m.Length));
                    return new ParserState(true, $"expecting a match for {s} at index {parserState.index}, got {checkString.Substring(10)}");
                });
        }

        public static Parser Letters(bool allowNone = false)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = parserState.index;
                    if (parserState.isError) return parserState;
                    string checkString = parserState.targetString.Substring(parserState.index);
                    Match m = allowNone ? Regex.Match(checkString, RegexStrings.lettersOrNone) : Regex.Match(checkString, RegexStrings.letters);
                    if (m.Success)
                        return new ParserState(parserState.index + m.Length, parserState.targetString, m.Value, new Tuple<int, int>(startIndex, parserState.index + m.Length));
                    return new ParserState(true, $"expecting a letter at index {parserState.index}, got {checkString[0]}");
                });
        }

        public static Parser Digits(bool allowNone = false)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = parserState.index;
                    if (parserState.isError) return parserState;
                    string checkString = parserState.targetString.Substring(parserState.index);
                    Match m = allowNone ? Regex.Match(checkString, RegexStrings.digitsOrNone) : Regex.Match(checkString, RegexStrings.digits);
                    if (m.Success)
                        return new ParserState(parserState.index + m.Length, parserState.targetString, m.Value, new Tuple<int, int>(startIndex, parserState.index + m.Length));
                    return new ParserState(true, $"expecting a digit at index {parserState.index}, got {checkString[0]}");
                });
        }

        public static Parser Match(string s)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = parserState.index;
                    if (parserState.isError) return parserState;
                    if (parserState.index >= parserState.targetString.Length)
                        return new ParserState(true, $"expecting {s} at index {parserState.index}, got end of string");

                    if (parserState.targetString.Substring(parserState.index).StartsWith(s))
                        return new ParserState(parserState.index + s.Length, parserState.targetString, s, new Tuple<int, int>(startIndex, parserState.index + s.Length));
                    return new ParserState(true, $"expecting {s} at index {parserState.index}, got {parserState.targetString.Substring(0, 1)}...");
                });
        }

        public static Parser LastEmpty()
        {
            return new Parser(
                (parserState) =>
                {
                    if (parserState.isError) return parserState;
                    if (parserState.targetString.Substring(parserState.index - 1).StartsWith(" ")) return parserState;
                    if (parserState.targetString.Substring(parserState.index - 1).StartsWith("\n")) return parserState;
                    if (parserState.targetString.Substring(parserState.index - 1).StartsWith("\t")) return parserState;
                    return new ParserState(true, $"expecting empty at index {parserState.index - 1}, got {parserState.targetString.Substring(0, 1)}...");
                });
        }


        public static Parser Not(string s)
        {
            return new Parser(
                (parserState) =>
                {
                    int startIndex = 0;
                    if (parserState.isError) return parserState;
                    if (parserState.targetString[parserState.index] == '\0')
                        return new ParserState(true, "unexpected end of string");
                    if (parserState.targetString.Length - parserState.index <= s.Length)
                        return new ParserState(parserState.index + 1, parserState.targetString, "", new Tuple<int, int>(startIndex, parserState.index + 1));
                    string checkString = parserState.targetString.Substring(parserState.index, s.Length);
                    if (checkString.Equals(s))
                        return new ParserState(true, $"not expecting {s} at index {parserState.index}");
                    return new ParserState(parserState.index + 1, parserState.targetString, checkString.Substring(0, 1), new Tuple<int, int>(startIndex, parserState.index + 1));
                });
        }

        public static Parser Choice(List<Parser> parsers)
        {
            return new Parser((parserState) =>
            {
                if (parserState.isError) return parserState;
                foreach (Parser p in parsers)
                {
                    ParserState nextState = p.Run(parserState);
                    if (!nextState.isError) return nextState;
                }

                return new ParserState(true, $"choice failed at index {parserState.index}");
            });
        }

        public static Parser Many(Parser parser, bool allowNone = false)
        {
            return new Parser((parserState) =>
            {
                List<object> indexes = new List<object>();
                if (parserState.isError) return parserState;
                List<object> result = new List<object>();
                ParserState nextState = parserState;
                if (!allowNone)
                {
                    nextState = parser.Run(parserState);
                    if (nextState.isError) return new ParserState(true, $"many failed at index {parserState.index}");
                    result.Add(nextState.result);
                    indexes.Add(nextState.indexes);
                }

                while (true)
                {
                    ParserState state = parser.Run(nextState);
                    if (state.isError) return new ParserState(nextState.index, nextState.targetString, result, indexes);
                    nextState = state;
                    result.Add(nextState.result);
                    indexes.Add(nextState.indexes);
                }
            });
        }

        public static Parser SequenceOf(List<Parser> parsers)
        {
            return new Parser((parserState) =>
            {
                if (parserState.isError) return parserState;
                List<object> results = new List<object>();
                List<object> indexes = new List<object>();
                ParserState nextState = parserState;
                foreach (Parser p in parsers)
                {
                    nextState = p.Run(nextState);
                    if (nextState.isError) return new ParserState(true, (string)nextState.result);
                    results.Add(nextState.result);
                    indexes.Add(nextState.indexes);
                }

                return new ParserState(nextState.index, nextState.targetString, results, indexes);
            });
        }

        public static Parser SeparetedBy(Parser valueParser, Parser separtorParser, bool allowNone = false)
        {
            return new Parser((parserState) =>
            {
                if (parserState.isError) return parserState;
                List<object> results = new List<object>();
                List<object> indexes = new List<object>();
                ParserState nextState = parserState;
                while (true)
                {
                    ParserState state = valueParser.Run(nextState);
                    if (state.isError) break;
                    results.Add(state.result);
                    indexes.Add(state.indexes);
                    nextState = state;
                    state = separtorParser.Run(nextState);
                    if (state.isError) break;
                    nextState = state;
                }

                if (!allowNone && results.Count == 0)
                    return new ParserState(true, $"separeted by failed at index {nextState.index}");
                return new ParserState(nextState.index, nextState.targetString, results, indexes);
            });
        }


        public static Parser Lazy(Lazy<Parser> parserGenerator)
        {
            return new Parser((parserState) =>
            {
                Parser parser = parserGenerator.Value;
                return parser.Run(parserState);
            });
        }

        public static Parser Success() =>
            new Parser((parserState) => parserState);

        public static Parser Error() =>
            new Parser((parserState) => new ParserState(true, $"errorParser at index {parserState.index}"));

        public static Parser Array(Parser value) =>
            Between(Match("{"), SeparetedBy(SequenceOf(new List<Parser>
            {
                Empty(true), value, Empty(true)
            }).Map(
                (result) => ((List<object>)result)[1],
                (indexes) => Extremes((List<object>)indexes)),
                Match(",")).Map(
                    (result) => result,
                    (indexes) => Extremes((List<object>)indexes)),
                Match("}"));

        public static Parser TypeWord() =>
                Choice(new List<Parser>
                {
                    Match("int"), Match("float"), Match("char"),
                    Match("double"), Match("long"), Match("short"),
                    Match("signed"), Match("unsigned"), Match("void"),
                    Match("auto"), Match("const"), Match("static"),
                    Match("volatile"), Match("extern"), Match("register")
                });

        public static Parser Type()
        {
            return new Parser((parserState) =>
            {
                int startIndex = parserState.index;
                Parser typesParser = Choice(new List<Parser>{
                        SeparetedBy(TypeWord(), Empty()).Map(
                            (result)=>result,
                            (indexes)=>Extremes((List<object>)indexes)),
                        SequenceOf(new List<Parser>
                        {
                            Choice(new List<Parser>{Match("struct"), Match("union"), Match("enum")}), Empty(), Variable(), Empty(true)
                        }).Map(
                            (result) => new List<object>{((List<object>)result)[0], ((List<object>)result)[2]},
                            (indexes) =>
                            {
                                ((List<object>)indexes).RemoveAt(3);
                                return Extremes((List<object>)indexes);
                            })
                    });
                ParserState nextState = typesParser.Run(parserState);
                if (nextState.isError)
                {
                    string checkString = parserState.targetString.Substring(parserState.index);
                    foreach (string s in types)
                    {
                        if (checkString.StartsWith(s))
                        {
                            char next = checkString.Substring(s.Length)[0];
                            int finalIndex = parserState.index + s.Length;
                            if (next == '*' || next == ')' || next == ' ' || next == '\n' || next == '\t')
                                return new ParserState(finalIndex, parserState.targetString, new List<object> { s }, new Tuple<int, int>(startIndex, finalIndex));
                        }
                    }
                    return nextState;
                }
                if (nextState.targetString.Substring(nextState.index).StartsWith("*") ||
                    nextState.targetString.Substring(nextState.index).StartsWith(")") ||
                     nextState.targetString.Substring(nextState.index - 1).StartsWith(" ") ||
                     nextState.targetString.Substring(nextState.index - 1).StartsWith("\n") ||
                     nextState.targetString.Substring(nextState.index - 1).StartsWith("\t")) return nextState;
                return new ParserState(true, $"types failed at index {parserState.index}");
            });
        }

        public static Parser Break() =>
            SequenceOf(new List<Parser> { Match("break"), Empty(true), Match(";") })
                .Map((result) => new List<object> { "break" },
                    (indexes) => Extremes((List<object>)indexes));


        public static Parser Continue() =>
            SequenceOf(new List<Parser> { Match("continue"), Empty(true), Match(";") })
                .Map((result) => new List<object> { "continue" },
                    (indexes) => Extremes((List<object>)indexes));

        public static Parser Enum() =>
            SequenceOf(new List<Parser> { Match("enum"), Empty(), Optional(Variable()).Map(
                    (result) =>
                    {
                        if (((string)result).Equals("void")) return "defaultName"; // id
                        return result;
                    },
                    (indexes) =>
                    {
                        if (((List<object>)indexes)[2] is Tuple<int, int>)
                            return Extremes((List<object>)indexes);
                        return ((List<object>)indexes)[0];
                    }), Empty(true), Array(Variable()) })
                .Map(
                    (result) => new List<object> { new List<object> { "enum", ((List<object>)result)[2] }, ((List<object>)result)[4] },
                    (indexes) => Extremes((List<object>)indexes));

        public static Parser EnumLine() =>
            SequenceOf(new List<Parser> { Enum(), Empty(true), Match(";") }).Map(
                (result) => ((List<object>)result)[0],
                (indexes) => Extremes((List<object>)indexes));

        public static Parser Integer() =>
            SequenceOf(new List<Parser>
            {
                Optional(Match("-")),
                Digits()
            }).Map(
                (result) =>
                {
                    string res = (string)((List<object>)result)[1];
                    if (((string)((List<object>)result)[0]).Equals("-")) res = "-" + (string)((List<object>)result)[1];
                    return new List<object> { "int", res };
                },
                (indexes) =>
                {
                    if (((List<object>)indexes)[0] is Tuple<int, int>)
                        return Extremes((List<object>)indexes);
                    return ((List<object>)indexes)[1];
                });

        public static Parser FloatingPoint() =>
            SequenceOf(new List<Parser>
            {
                Optional(Match("-")),
                Digits(),
                Match("."),
                Digits(true)
            }).Map(
                (result) =>
                {
                    string res = (string)((List<object>)result)[1];
                    if (((string)((List<object>)result)[0]).Equals("-")) res = "-" + (string)((List<object>)result)[1];
                    if (((string)((List<object>)result)[3]).Length > 0) res += "." + (string)((List<object>)result)[3];
                    else res += ".0";
                    return new List<object> { "float", res };
                },
                (indexes) =>
                {
                    if (((List<object>)indexes)[0] is Tuple<int, int>)
                        return Extremes((List<object>)indexes);
                    ((List<object>)indexes).RemoveAt(0);
                    return Extremes((List<object>)indexes);
                });

        public static Parser Character() =>
            Between(Match("'"), Not("'")).Map(
                (result) => new List<object> { "char", result },
                (indexes) => indexes);

        public static Parser String() =>
            Between(Match("\""), Many(Not("\""))).Map((result) => result, (indexes) => Extremes((List<object>)indexes))
                .Map(
                    (result) => new List<object> { "string", string.Join("", (List<object>)result) },
                    (indexes) => Extremes((List<object>)indexes));

        public static Parser ScalarValue() =>
            Choice(new List<Parser>
            {
                FloatingPoint(),
                Integer(),
                Character()
            });

        public static Parser BinaryOperator() =>
            Choice(new List<Parser>
            {
                Match("+"), Match("-"), Match("*"),
                Match("/"), Match("%"), Match(">="),
                Match("<="), Match(">"), Match("<"),
                Match("=="), Match("!="), Match("&&"),
                Match("||"), Match("&"), Match("|"),
                Match("^"), Match("=")
            });

        public static Parser UnaryOperator() =>
            Choice(new List<Parser>
            {
                Match("++"), Match("--")
            });

        public static Parser AssigmentsSigns() =>
            Choice(new List<Parser>
            {
                Match("+="), Match("-="), Match("*="),
                Match("/="), Match("%="), Match("&&="),
                Match("||="), Match("&="), Match("|="),
                Match("^=")
            });

        public static Parser InLineComment() =>
            SequenceOf(new List<Parser>
            {
                Match("//"), Many(Not("\n"), true), Choice(new List<Parser>{Match("\n"), End()})
            }).Map(
                (result) => new List<object> { "comment" },
                (indexes) => Extremes((List<object>)indexes));

        public static Parser ManyLinesComment() =>
            Between(Match("/*"), Many(Not("*/")), Match("*/"))
                .Map(
                    (result) => new List<object> { "comment" },
                    (indexes) => indexes);

        public static Parser Label() =>
           SequenceOf(new List<Parser> { Variable(), Empty(true), Match(":") })
               .Map(
                   (result) => new List<object> { "label", ((List<object>)result)[0] },
                   (indexes) => new List<object> { "label", Extremes((List<object>)indexes) });
        public static Parser Goto() =>
            SequenceOf(new List<Parser> { Match("goto"), Empty(), Variable(), Empty(true), Match(";") })
                .Map(
                    (result) => new List<object> { "goto", ((List<object>)result)[2] },
                    (indexes) => Extremes((List<object>)indexes));

        public static Parser SizeOf() =>
            SequenceOf(new List<Parser>
            {
                Match("sizeof").Map(
                    (result) => new List<object> { "fucntion", "sizeof" },
                    (indexes) => indexes),
                Empty(true),
                Between(Match("("), SeparetedBy(Choice(new List<Parser>
                {
                    ExistingVariable(), Type(), ScalarValue(), String()
                }), SequenceOf(new List<Parser> { Empty(true), Match(","), Empty(true) })), Match(")"))
            }).Map(
                (result) => result,
                (indexes) => Extremes((List<object>)indexes));

        public static Parser FunctionCall(Parser functionCallParser)
        {
            return new Parser((parserState) =>
            {
                int startIndex = parserState.index;
                string checkString = parserState.targetString.Substring(parserState.index);
                foreach (string s in functions)
                    if (checkString.StartsWith(s))
                    {
                        char next = checkString.Substring(s.Length)[0];
                        if (next == '(' || next == '\n' || next == ' ' || next == '\t')
                        {
                            parserState.index += s.Length;
                            ParserState nextState = functionCallParser.Run(parserState);
                            if (nextState.isError) return new ParserState(true, "error in function call");
                            List<object> result = new List<object>
                                { "functionCall", s, ((List<object>)nextState.result)[1] };
                            return new ParserState(nextState.index, parserState.targetString, result, new Tuple<int, int>(startIndex, nextState.index));
                        }
                    }
                return new ParserState(true, "no such function");
            });
        }

        public static Tuple<int, int> Extremes(List<object> l)
        {
            int length = l.Count;
            return new Tuple<int, int>(((Tuple<int, int>)l[0]).Item1, ((Tuple<int, int>)l[length - 1]).Item2);
        }
    }

    public struct ParserState
    {
        public int index;
        public string targetString;
        public object result;
        public object indexes;
        public bool isError;

        public ParserState(string s)
        {
            index = 0;
            targetString = s + "\0";
            result = null;
            isError = false;
            indexes = null;
        }

        public ParserState(int i, string s, object r, object ind)
        {
            index = i;
            targetString = s;
            result = r;
            isError = false;
            indexes = ind;
        }

        public ParserState(bool e, string message)
        {
            index = 0;
            targetString = null;
            result = message;
            isError = e;
            indexes = null;
        }

        public override string ToString()
        {
            if (isError) return $"error={result}";
            return $"{{object={targetString}, index={index}, result={Decode(result)}}}";
        }

        public string Result() => Decode(result);

        public string Indexes() => Decode(indexes);

        public static string Decode(object obj)
        {
            if (obj is string) return (string)obj;
            if (obj is double) return ((double)obj).ToString();
            if (obj is int) return ((int)obj).ToString();
            if (obj is Tuple<int, int>)
                return "(" + ((Tuple<int, int>)obj).Item1.ToString() + ", " + ((Tuple<int, int>)obj).Item2.ToString() + ")";
            string ris = "[";
            foreach (object o in (List<object>)obj)
                ris += Decode(o) + ",";
            return ris.Length == 1 ? ris + "]" : ris.Substring(0, ris.Length - 1) + "]";
        }
    }
}