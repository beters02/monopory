public class MakeBoardSpaces : Component
{

    [Property] public GameObject SectionsGameObject { get; set; }
    
    [Button("Run it")]
    public void TempBoardSpaces()
    {

        if ( SectionsGameObject is null )
        {
            Log.Info("is null");
            return;
        }
        
        List<List<string>> Sections = [
            [
                "1_property_brown_0",
                "2_chest_0",
                "3_property_brown_1",
                "4_tax_income",
            ],
            [
                "6_property_light_blue_0",
                "7_chance_0",
                "8_property_light_blue_1",
                "9_property_light_blue_2",
            ],
            [
                "11_property_pink_0",
                "12_utility_0",
                "13_property_pink_1",
                "14_property_pink_2",
            ],
            [
                "16_property_orange_0",
                "17_chest_1",
                "18_property_orange_1",
                "19_property_orange_2",
            ],
            [
                "21_property_red_0",
                "22_chance_1",
                "23_property_red_1",
                "24_property_red_2",
            ],
            [
                "26_property_yellow_0",
                "27_property_yellow_1",
                "28_utility_1",
                "29_property_yellow_2",
            ],
            [
                "31_property_green_0",
                "32_property_green_1",
                "33_chest_2",
                "34_property_green_2",
            ],
            [
                "36_chance_2",
                "37_property_dark_blue_0",
                "38_tax_luxury",
                "39_property_dark_blue_1",
            ]
        ];

        List<List<string>> Connectors = [
            [
                "0_go"
            ],
            [
                "5_railroad_0",
            ],
            [
                "10_jail",
            ],
            [
                "15_railroad_1",
            ],
            [
                "20_free_parking",
            ],
            [
                "25_railroad_2",
            ],
            [
                "30_go_to_jail",
            ],
            [
                "35_railroad_3",
            ]
        ];
        
        int connectorsCounter = -1;
        int sectionsCounter = -1;
        foreach(List<string> idList in Sections)
        {
            sectionsCounter++;
            connectorsCounter++;

            if( connectorsCounter + 1 <= Connectors.Count )
            {
                var cgo = new GameObject(SectionsGameObject, true, "Connector_" + connectorsCounter);
                foreach( string connectorId in Connectors[connectorsCounter] )
                {
                    new GameObject(cgo, true, connectorId);
                }
            }

            var sgo = new GameObject(SectionsGameObject, true, "Section_" + sectionsCounter);

            foreach(string propId in idList)
            {
                new GameObject(sgo, true, propId);
            }
        }

    }



}