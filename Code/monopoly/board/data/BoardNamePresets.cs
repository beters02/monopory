using System;
using System.Collections.Generic;
using System.Linq;

public static class BoardNamePresets
{
	public const string DefaultPresetId = "board_default";
	public const string ClassicPresetId = "board_classic";
	public const string RentRushPresetId = "board_rent_rush";

	[BoardSpaceNamePreset( DefaultPresetId, "Default Names", 0 )]
	public static class DefaultNames { }

	[BoardSpaceNamePreset( ClassicPresetId, "Classic Names", 10 )]
	public static class ClassicNames
	{
		[BoardSpaceNameModifier( "property_brown_0" )]
		public static readonly string MediterraneanAvenue = "Mediterranean Avenue";

		[BoardSpaceNameModifier( "property_brown_1" )]
		public static readonly string BalticAvenue = "Baltic Avenue";

		[BoardSpaceNameModifier( "tax_income" )]
		public static readonly string IncomeTax = "Income Tax";

		[BoardSpaceNameModifier( "railroad_0" )]
		public static readonly string ReadingRailroad = "Reading Railroad";

		[BoardSpaceNameModifier( "property_light_blue_0" )]
		public static readonly string OrientalAvenue = "Oriental Avenue";

		[BoardSpaceNameModifier( "property_light_blue_1" )]
		public static readonly string VermontAvenue = "Vermont Avenue";

		[BoardSpaceNameModifier( "property_light_blue_2" )]
		public static readonly string ConnecticutAvenue = "Connecticut Avenue";

		[BoardSpaceNameModifier( "property_pink_0" )]
		public static readonly string StCharlesPlace = "St. Charles Place";

		[BoardSpaceNameModifier( "utility_0" )]
		public static readonly string ElectricCompany = "Electric Company";

		[BoardSpaceNameModifier( "property_pink_1" )]
		public static readonly string StatesAvenue = "States Avenue";

		[BoardSpaceNameModifier( "property_pink_2" )]
		public static readonly string VirginiaAvenue = "Virginia Avenue";

		[BoardSpaceNameModifier( "railroad_1" )]
		public static readonly string PennsylvaniaRailroad = "Pennsylvania Railroad";

		[BoardSpaceNameModifier( "property_orange_0" )]
		public static readonly string StJamesPlace = "St. James Place";

		[BoardSpaceNameModifier( "property_orange_1" )]
		public static readonly string TennesseeAvenue = "Tennessee Avenue";

		[BoardSpaceNameModifier( "property_orange_2" )]
		public static readonly string NewYorkAvenue = "New York Avenue";

		[BoardSpaceNameModifier( "property_red_0" )]
		public static readonly string KentuckyAvenue = "Kentucky Avenue";

		[BoardSpaceNameModifier( "property_red_1" )]
		public static readonly string IndianaAvenue = "Indiana Avenue";

		[BoardSpaceNameModifier( "property_red_2" )]
		public static readonly string IllinoisAvenue = "Illinois Avenue";

		[BoardSpaceNameModifier( "railroad_2" )]
		public static readonly string BOrailroad = "B&O Railroad";

		[BoardSpaceNameModifier( "property_yellow_0" )]
		public static readonly string AtlanticAvenue = "Atlantic Avenue";

		[BoardSpaceNameModifier( "property_yellow_1" )]
		public static readonly string VentnorAvenue = "Ventnor Avenue";

		[BoardSpaceNameModifier( "utility_1" )]
		public static readonly string WaterWorks = "Water Works";

		[BoardSpaceNameModifier( "property_yellow_2" )]
		public static readonly string MarvinGardens = "Marvin Gardens";

		[BoardSpaceNameModifier( "property_green_0" )]
		public static readonly string PacificAvenue = "Pacific Avenue";

		[BoardSpaceNameModifier( "property_green_1" )]
		public static readonly string NorthCarolinaAvenue = "North Carolina Avenue";

		[BoardSpaceNameModifier( "property_green_2" )]
		public static readonly string PennsylvaniaAvenue = "Pennsylvania Avenue";

		[BoardSpaceNameModifier( "railroad_3" )]
		public static readonly string ShortLine = "Short Line";

		[BoardSpaceNameModifier( "property_dark_blue_0" )]
		public static readonly string ParkPlace = "Park Place";

		[BoardSpaceNameModifier( "tax_luxury" )]
		public static readonly string LuxuryTax = "Luxury Tax";

		[BoardSpaceNameModifier( "property_dark_blue_1" )]
		public static readonly string Boardwalk = "Boardwalk";
	}

	[BoardSpaceNamePreset( RentRushPresetId, "Rent Rush Names", 20 )]
	public static class RentRushNames
	{
		[BoardSpaceNameModifier( "go" )]
		public static readonly string Go = "Landing";

		[BoardSpaceNameModifier( "property_brown_0" )]
		public static readonly string StudioApartment = "Studio Apartment";

		[BoardSpaceNameModifier( "chest_0" )]
		public static readonly string CommunityChest0 = "Community Chest";

		[BoardSpaceNameModifier( "property_brown_1" )]
		public static readonly string LaundryLofts = "Laundry Lofts";

		[BoardSpaceNameModifier( "tax_income" )]
		public static readonly string PlugTax = "Plug Tax";

		[BoardSpaceNameModifier( "railroad_0" )]
		public static readonly string TransitHub = "Transit Hub";

		[BoardSpaceNameModifier( "property_light_blue_0" )]
		public static readonly string CollegeCommons = "College Commons";

		[BoardSpaceNameModifier( "chance_0" )]
		public static readonly string Chance0 = "Chance";

		[BoardSpaceNameModifier( "property_light_blue_1" )]
		public static readonly string DeMiraq = "de_Miraq";

		[BoardSpaceNameModifier( "property_light_blue_2" )]
		public static readonly string DeNuke = "de_Nuke";

		[BoardSpaceNameModifier( "jail" )]
		public static readonly string VisitingEvictionCourt = "Visiting Eviction Court";

		[BoardSpaceNameModifier( "property_pink_0" )]
		public static readonly string DowntownDistrict = "Downtown District";

		[BoardSpaceNameModifier( "utility_0" )]
		public static readonly string PowerGrid = "Power Grid";

		[BoardSpaceNameModifier( "property_pink_1" )]
		public static readonly string MarketSquare = "Market Square";

		[BoardSpaceNameModifier( "property_pink_2" )]
		public static readonly string CanalsDistrict = "Canals District";

		[BoardSpaceNameModifier( "railroad_1" )]
		public static readonly string MetroLine = "Metro Line";

		[BoardSpaceNameModifier( "property_orange_0" )]
		public static readonly string RiversideVillas = "Riverside Villas";

		[BoardSpaceNameModifier( "chest_1" )]
		public static readonly string CommunityChest1 = "Community Chest";

		[BoardSpaceNameModifier( "property_orange_1" )]
		public static readonly string Harbor17 = "Harbor 17";

		[BoardSpaceNameModifier( "property_orange_2" )]
		public static readonly string SkylineTowers = "Skyline Towers";

		[BoardSpaceNameModifier( "free_parking" )]
		public static readonly string FreeParking = "Free Parking";

		[BoardSpaceNameModifier( "property_red_0" )]
		public static readonly string BlackMesaBusinessPark = "Black Mesa Business Park";

		[BoardSpaceNameModifier( "chance_1" )]
		public static readonly string Chance1 = "Chance";

		[BoardSpaceNameModifier( "property_red_1" )]
		public static readonly string LambdaSquare = "Lambda Square";

		[BoardSpaceNameModifier( "property_red_2" )]
		public static readonly string RavenholmHeights = "Ravenholm Heights";

		[BoardSpaceNameModifier( "railroad_2" )]
		public static readonly string ExpressLine = "Express Line";

		[BoardSpaceNameModifier( "property_yellow_0" )]
		public static readonly string KleinerCommons = "Kleiner Commons";

		[BoardSpaceNameModifier( "property_yellow_1" )]
		public static readonly string WhiteForestEstates = "White Forest Estates";

		[BoardSpaceNameModifier( "utility_1" )]
		public static readonly string InternetProvider = "Internet Provider";

		[BoardSpaceNameModifier( "property_yellow_2" )]
		public static readonly string VertigoTowers = "Vertigo Towers";

		[BoardSpaceNameModifier( "go_to_jail" )]
		public static readonly string Evicted = "Evicted!";

		[BoardSpaceNameModifier( "property_green_0" )]
		public static readonly string ConstructCourt = "Construct Court";

		[BoardSpaceNameModifier( "property_green_1" )]
		public static readonly string City17Condos = "City 17 Condos";

		[BoardSpaceNameModifier( "chest_2" )]
		public static readonly string CommunityChest2 = "Community Chest";

		[BoardSpaceNameModifier( "property_green_2" )]
		public static readonly string NovaProspektVillas = "Nova Prospekt Villas";

		[BoardSpaceNameModifier( "railroad_3" )]
		public static readonly string RapidTransit = "Rapid Transit";

		[BoardSpaceNameModifier( "chance_2" )]
		public static readonly string Chance2 = "Chance";

		[BoardSpaceNameModifier( "property_dark_blue_0" )]
		public static readonly string FacepunchPlaza = "Facepunch Plaza";

		[BoardSpaceNameModifier( "tax_luxury" )]
		public static readonly string HoaFine = "HOA Fine";

		[BoardSpaceNameModifier( "property_dark_blue_1" )]
		public static readonly string BillionaireBoulevard = "Billionaire Boulevard";
	}
}

[AttributeUsage( AttributeTargets.Class )]
public sealed class BoardSpaceNamePresetAttribute : MatchSettingsPresetAttribute
{
	public BoardSpaceNamePresetAttribute( string id, string name, int order = 0 ) : base( id, name, order )
	{
	}

	protected override string CreateSnapshot( TypeDescription type )
	{
		var modifiers = new BoardSpaceNameModifiersObject();
		foreach ( var field in GetModifierFields<BoardSpaceNameModifierAttribute>( type ) )
		{
			var attr = field.GetCustomAttribute<BoardSpaceNameModifierAttribute>();
			modifiers.Add( attr.Build( field ) );
		}

		return BoardSpaceNameConfig.SerializeModifiers( modifiers );
	}
}

[AttributeUsage( AttributeTargets.Field )]
public sealed class BoardSpaceNameModifierAttribute : Attribute
{
	public string Id { get; }

	public BoardSpaceNameModifierAttribute( string id )
	{
		Id = id;
	}

	public BoardSpaceNameModifier Build( FieldDescription field )
	{
		return new( Id, field.GetValue( null )?.ToString() ?? "" );
	}
}
