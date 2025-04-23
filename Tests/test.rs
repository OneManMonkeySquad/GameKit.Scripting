
fn on_update() {
    // print(entity);
    // print(elapsed_time);

    set_local_transform(entity, -1854.5, as_float(273.32 + sin(elapsed_time * 3)), 1208.8);

    if (get_health(entity) <= 0)
    {
        print("SHIP DEAD");
    }
}