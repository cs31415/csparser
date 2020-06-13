namespace csParser
{
    /// <summary>
    /// The value part of the argMap.txt file (key is method name)
    /// </summary>
    class ArgMapRecord
    {
        public string MethodName { get;set; }
        public int ArgIdx { get; set; }
        public string NameSpace { get; set; }
    }
}
