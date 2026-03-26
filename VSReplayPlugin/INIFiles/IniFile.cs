using System.IO;
using System.Text;
using System.Globalization;

namespace Framework.IniFiles
{
  public class IniFile
  {
    Dictionary<string, Dictionary<string, string>> ini = new Dictionary<string, Dictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
    string file;

    /// <summary>
    /// Initialize an INI file
    /// Load it if it exists
    /// </summary>
    /// <param name="file">Full path where the INI file has to be read from or written to</param>
    public IniFile( string file,bool isContent = false )
    {
        if (!isContent)
        {
            this.file = file;

            if (!File.Exists(file))
                return;

            Load(File.ReadAllText(file));
        }
        else
        {
            Load(file);
        }
    }

    /// <summary>
    /// Load the INI file content
    /// </summary>
    public void Load( string txt )
    {
      Dictionary<string, string> currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

      ini[""] = currentSection;

      foreach( var l in txt.Split( new[] { "\n" },StringSplitOptions.RemoveEmptyEntries )
                        .Select( ( t,i ) => new
                        {
                          idx = i,
                          text = t.Trim( )
                        } ) )
      // .Where(t => !string.IsNullOrWhiteSpace(t) && !t.StartsWith(";")))
      {
        var line = l.text;

        if( line.StartsWith( ";" ) || string.IsNullOrWhiteSpace( line ) )
        {
          currentSection.Add( ";" + l.idx.ToString( ),line );
          continue;
        }

        if( line.StartsWith( "[" ) && line.EndsWith( "]" ) )
        {
          currentSection = new Dictionary<string,string>( StringComparer.InvariantCultureIgnoreCase );
          ini[line.Substring( 1,line.Length - 2 )] = currentSection;
          continue;
        }

        var idx = line.IndexOf("=");
        if( idx == -1 )
          currentSection[line] = "";
        else
          currentSection[line.Substring( 0,idx )] = line.Substring( idx + 1 );
      }
    }

    /// <summary>
    /// Get a parameter value at the root level
    /// </summary>
    /// <param name="key">parameter key</param>
    /// <returns></returns>
    public string? GetValue( string key )
    {
      return GetValue( key,"","" );
    }

    /// <summary>
    /// Returns a parameter value in the section, with a default value if not found
    /// </summary>
    /// <param name="key">parameter key</param>
    /// <param name="section">section</param>
    /// <param name="default">default value</param>
    /// <returns></returns>
    public string? GetValue( string key,string section,string? @default = null)
    {
      if( !ini.ContainsKey( section ) )
        return @default;

      if( !ini[section].ContainsKey( key ) )
        return @default;

      return ini[section][key];
    }

    public bool GetBoolValue( string key,string section,bool @default = false )
    {
      if( !ini.ContainsKey( section ) )
        return @default;

      if( !ini[section].ContainsKey( key ) )
        return @default;

      return ini[section][key] == "True" || ini[section][key] == "true";
    }
    public int GetIntValue( string key,string section,int @default = 0 )
    {
      if( !ini.ContainsKey( section ) )
        return @default;

      if( !ini[section].ContainsKey( key ) )
        return @default;

      if( int.TryParse( ini[section][key],out int value ) )
        return value;
      return @default;
    }
    public float GetFloatValue( string key,string section,float @default = 0 )
    {
      if( !ini.ContainsKey( section ) )
        return @default;

      if( !ini[section].ContainsKey( key ) )
        return @default;

      if( float.TryParse( ini[section][key].Replace( ',','.' ),new CultureInfo( "en-US" ),out float f ) )
        return f;
      return @default;
    }
    public double GetDoubleValue( string key,string section,double @default = 0 )
    {
      if( !ini.ContainsKey( section ) )
        return @default;

      if( !ini[section].ContainsKey( key ) )
        return @default;

      if( double.TryParse( ini[section][key].Replace( ',','.' ),new CultureInfo( "en-US" ),out double d ) )
        return d;
      return @default;
    }

    /// <summary>
    /// Save the INI file
    /// </summary>
    public void Save( )
    {
      var sb = new StringBuilder();
      foreach( var section in ini )
      {
        if( section.Key != "" )
        {
          sb.AppendFormat( "[{0}]",section.Key );
          sb.AppendLine( );
        }

        foreach( var keyValue in section.Value )
        {
          if( keyValue.Key.StartsWith( ";" ) )
          {
            sb.Append( keyValue.Value );
            sb.AppendLine( );
          }
          else
          {
            sb.AppendFormat( "{0}={1}",keyValue.Key,keyValue.Value );
            sb.AppendLine( );
          }
        }

        if( !endWithCRLF( sb ) )
          sb.AppendLine( );
      }

      File.WriteAllText( file,sb.ToString( ) );
    }

    public void SaveAs( string filename )
    {
      file = filename;

      Save( );
    }

    bool endWithCRLF( StringBuilder sb )
    {
      if( sb.Length < 4 )
      {
        if( sb.Length > 1 )
          return sb[sb.Length - 2] == '\r' &&
                 sb[sb.Length - 1] == '\n';
        return false;
      }
      else
        return sb[sb.Length - 4] == '\r' &&
               sb[sb.Length - 3] == '\n' &&
               sb[sb.Length - 2] == '\r' &&
               sb[sb.Length - 1] == '\n';
    }

    /// <summary>
    /// Write a parameter value at the root level
    /// </summary>
    /// <param name="key">parameter key</param>
    /// <param name="value">parameter value</param>
    public void WriteValue( string key,string value )
    {
      WriteValue( key,"",value );
    }

    /// <summary>
    /// Write a parameter value in a section
    /// </summary>
    /// <param name="key">parameter key</param>
    /// <param name="section">section</param>
    /// <param name="value">parameter value</param>
    public void WriteValue( string key,string section,string value )
    {
      Dictionary<string, string> currentSection;
      if( !ini.ContainsKey( section ) )
      {
        currentSection = new Dictionary<string,string>( );
        ini.Add( section,currentSection );
      }
      else
        currentSection = ini[section];

      currentSection[key] = value;
    }
    public void WriteValue( string key,string section,int value )
    {
      Dictionary<string, string> currentSection;
      if( !ini.ContainsKey( section ) )
      {
        currentSection = new Dictionary<string,string>( );
        ini.Add( section,currentSection );
      }
      else
        currentSection = ini[section];

      currentSection[key] = value.ToString( );
    }
    public void WriteValue( string key,string section,bool value )
    {
      Dictionary<string, string> currentSection;
      if( !ini.ContainsKey( section ) )
      {
        currentSection = new Dictionary<string,string>( );
        ini.Add( section,currentSection );
      }
      else
        currentSection = ini[section];

      currentSection[key] = value.ToString( );
    }
    public void WriteValue( string key,string section,float value )
    {
      Dictionary<string, string> currentSection;
      if( !ini.ContainsKey( section ) )
      {
        currentSection = new Dictionary<string,string>( );
        ini.Add( section,currentSection );
      }
      else
        currentSection = ini[section];

      currentSection[key] = String.Format( "{0}",value );
    }
    public void WriteValue( string key,string section,double value )
    {
      Dictionary<string, string> currentSection;
      if( !ini.ContainsKey( section ) )
      {
        currentSection = new Dictionary<string,string>( );
        ini.Add( section,currentSection );
      }
      else
        currentSection = ini[section];

      currentSection[key] = String.Format( "{0}",value );
    }

    /// <summary>
    /// Get all the keys names in a section
    /// </summary>
    /// <param name="section">section</param>
    /// <returns></returns>
    public string[] GetKeys( string section )
    {
      if( !ini.ContainsKey( section ) )
        return new string[0];

      return ini[section].Keys.ToArray( );
    }

    /// <summary>
    /// Get all the section names of the INI file
    /// </summary>
    /// <returns></returns>
    public string[] GetSections( )
    {
      return ini.Keys.Where( t => t != "" ).ToArray( );
    }
  }
}
