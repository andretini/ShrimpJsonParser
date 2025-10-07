string json = "{\"name\":\"Henrick\",\"alive\":true,\"score\":12.5,\"tags\":[\"dev\",\"ios\"],\"nil\":null}";
var data = SimpleJson.Parse(json);


var obj = (Dictionary<string, object>)data;
Console.WriteLine(obj["name"]);       // Henrick
Console.WriteLine((bool)obj["alive"]); // True
Console.WriteLine((decimal)obj["score"]); // 12.5

var tags = (List<object>)obj["tags"];
Console.WriteLine(tags[0]);           // dev