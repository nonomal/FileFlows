namespace FileFlows.Shared.Models;

/// <summary>
/// A script is a special function node that lets you reuse them
/// </summary>
public class Script:FileFlowObject
{   
    /// <summary>
    /// Gets or sets the javascript code of the script
    /// </summary>
    public string Code { get; set; }
}


/// <summary>
/// A parsed version of a Script object 
/// </summary>
public class ScriptModel
{
    /// <summary>
    /// Gets or sets the name of the script
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the code of the script
    /// </summary>
    public string Code { get; set; }
    
    /// <summary>
    /// Gets or sets the Author who wrote this script
    /// </summary>
    public string Author { get; set; }
    
    /// <summary>
    /// Gets or sets the description of this script
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Gets or sets a list of outputs for the script
    /// </summary>
    public List<ScriptOutput> Outputs { get; set; }

    /// <summary>
    /// Gets or sets parameters for the script
    /// </summary>
    public List<ScriptParameter> Parameters { get; set; }

}

/// <summary>
/// Definition of a script output node
/// </summary>
public class ScriptOutput
{
    /// <summary>
    /// Gets or sets the output index
    /// </summary>
    public int Index { get; set; }
    
    /// <summary>
    /// Gets or sets the description of the output
    /// </summary>
    public string Description { get; set; }
}

/// <summary>
/// A parameter passed into a script
/// </summary>
public class ScriptParameter
{
    /// <summary>
    /// Gets or sets the name of the parameter
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the parameter
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Gets or sets the type of script argument
    /// </summary>
    public ScriptArgumentType Type {get; set; }
}

/// <summary>
/// Types of script parameters
/// </summary>
public enum ScriptArgumentType
{
    /// <summary>
    /// String parameter
    /// </summary>
    String, 
    /// <summary>
    /// Integer parameter
    /// </summary>
    Int,
    /// <summary>
    /// Boolean parameter
    /// </summary>
    Bool
}