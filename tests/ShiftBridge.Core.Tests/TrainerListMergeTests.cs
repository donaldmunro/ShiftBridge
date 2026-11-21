using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Platform.Trainer;
using ShiftBridge.Presentation;

namespace ShiftBridge.Core.Tests;

public class TrainerListMergeTests
{
    private static DiscoveredBleTrainer T(ulong address, string name, short rssi = -60, TrainerProtocols p = TrainerProtocols.Ftms) =>
        new(address, name, rssi, p);

    private static (ObservableCollection<DiscoveredBleTrainer> List, List<NotifyCollectionChangedAction> Events) Watch(params DiscoveredBleTrainer[] items)
    {
        var list = new ObservableCollection<DiscoveredBleTrainer>(items);
        var events = new List<NotifyCollectionChangedAction>();
        list.CollectionChanged += (_, e) => events.Add(e.Action);
        return (list, events);
    }

    [Fact]
    public void An_unchanged_scan_touches_nothing()
    {
        var (list, events) = Watch(T(1, "Hub"), T(2, "Kickr"));
        TrainerListMerge.Apply(list, [T(1, "Hub"), T(2, "Kickr")], []);
        Assert.Empty(events); // aucun clignotement
        Assert.Equal(["Hub", "Kickr"], list.Select(t => t.Name));
    }

    [Fact]
    public void New_trainers_are_inserted_in_name_order()
    {
        var (list, events) = Watch(T(1, "Hub"), T(3, "Tacx"));
        TrainerListMerge.Apply(list, [T(1, "Hub"), T(2, "Kickr"), T(3, "Tacx")], []);
        Assert.Equal(["Hub", "Kickr", "Tacx"], list.Select(t => t.Name));
        Assert.Equal([NotifyCollectionChangedAction.Add], events);
    }

    [Fact]
    public void A_changed_trainer_is_replaced_in_place_without_moving_the_others()
    {
        var (list, events) = Watch(T(1, "Hub"), T(2, "Kickr"));
        TrainerListMerge.Apply(list, [T(1, "Hub"), T(2, "Kickr", p: TrainerProtocols.Ftms | TrainerProtocols.FecOverBle)], []);
        Assert.Equal([NotifyCollectionChangedAction.Replace], events);
        Assert.Equal(TrainerProtocols.Ftms | TrainerProtocols.FecOverBle, list[1].Protocols);
    }

    [Fact]
    public void A_trainer_not_seen_any_more_is_removed_unless_it_is_kept()
    {
        var (list, _) = Watch(T(1, "Hub"), T(2, "Kickr"), T(3, "Tacx"));
        TrainerListMerge.Apply(list, [T(1, "Hub")], [T(3, "Tacx")]);
        Assert.Equal(["Hub", "Tacx"], list.Select(t => t.Name)); // Kickr disparaît, Tacx (mémorisé) reste
    }

    [Fact]
    public void The_remembered_trainer_stays_when_the_scan_starts_empty()
    {
        var saved = T(7, "Mon KICKR", rssi: 0, p: TrainerProtocols.None);
        var (list, events) = Watch(saved);
        TrainerListMerge.Apply(list, [], [saved, saved]);
        Assert.Single(list);
        Assert.Empty(events);
    }

    [Fact]
    public void What_the_scan_sees_replaces_the_remembered_placeholder()
    {
        var saved = T(7, "Dernier trainer", rssi: 0, p: TrainerProtocols.None);
        var (list, _) = Watch(saved);
        TrainerListMerge.Apply(list, [T(7, "KICKR 1234", -48)], [saved]);
        Assert.Single(list);
        Assert.Equal("KICKR 1234", list[0].Name);
        Assert.Equal(-48, list[0].Rssi);
    }

    [Fact]
    public void A_renamed_trainer_moves_to_its_new_place()
    {
        var (list, _) = Watch(T(1, "Alpha"), T(2, "Beta"), T(3, "Gamma"));
        TrainerListMerge.Apply(list, [T(1, "Alpha"), T(2, "Beta"), T(3, "Aardvark")], []);
        Assert.Equal(["Aardvark", "Alpha", "Beta"], list.Select(t => t.Name));
    }

    [Fact]
    public void The_selected_trainer_survives_a_scan_that_does_not_see_it()
    {
        var chosen = T(5, "Mon trainer");
        var (list, _) = Watch(chosen, T(1, "Autre"));
        TrainerListMerge.Apply(list, [T(1, "Autre")], [chosen]);
        Assert.Contains(list, t => t.Address == 5);
    }
}
